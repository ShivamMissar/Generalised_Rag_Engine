using Microsoft.Extensions.AI;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Indexing;

/// <summary>
/// The ingestion pipeline: takes loaded documents, cuts them into chunks, fingerprints each chunk and saves
/// the results in a vector store, ready to be searched.
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b> this is "putting the books on the shelf". It runs the steps in order:</para>
/// <code>
/// documents → chunker → chunks → embedding model → fingerprinted chunks → vector store
/// </code>
/// <para><b>Re-indexing is safe:</b> indexing a document again replaces all of its old chunks. Documents that
/// aren't passed in are left alone. If fingerprinting fails (for example, Ollama isn't running), the store
/// isn't touched, because every chunk is fingerprinted before anything is deleted or written.</para>
/// </remarks>
public sealed class DocumentIndexer(
    IChunker chunker,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IVectorStore store,
    EmbeddingPrefixes prefixes)
{
    // How many chunks to send to the embedding model per request. Batching is much faster than one request per
    // chunk, and small batches keep each request quick.
    private const int BatchSize = 16;

    /// <summary>
    /// Chunks, fingerprints and stores <paramref name="documents"/> in <paramref name="collection"/>,
    /// creating the collection if needed.
    /// </summary>
    public async Task<IndexingResult> IndexAsync(
        Collection collection,
        IReadOnlyList<Document> documents,
        CancellationToken cancellationToken = default)
    {
        // 1. Cut every document into chunks.
        var chunks = documents.SelectMany(document => chunker.Chunk(document)).ToList();

        // 2. Fingerprint the chunks in batches. Each chunk's text gets the "document" label first (see EmbeddingPrefixes).
        var embedded = new List<Chunk>(chunks.Count);
        foreach (var batch in chunks.Chunk(BatchSize))
        {
            var embeddings = await embedder.GenerateAsync(
                batch.Select(chunk => prefixes.Document + chunk.Content),
                cancellationToken: cancellationToken);

            if (embeddings.Count != batch.Length)
            {
                throw new InvalidOperationException($"Asked for {batch.Length} embeddings but got {embeddings.Count}.");
            }

            for (var i = 0; i < batch.Length; i++)
            {
                // Chunks are immutable records, so "with" makes a copy that has the embedding attached.
                embedded.Add(batch[i] with { Embedding = embeddings[i].Vector });
            }
        }

        // 3. Save: create the collection if needed, remove the documents' old chunks, then write the new ones.
        await store.EnsureCollectionAsync(collection, cancellationToken);
        await store.DeleteDocumentsAsync(collection.Name, documents.Select(document => document.Id), cancellationToken);
        await store.UpsertAsync(collection.Name, embedded, cancellationToken);

        return new IndexingResult(documents.Count, embedded);
    }
}

/// <summary>What <see cref="DocumentIndexer.IndexAsync"/> did.</summary>
/// <param name="DocumentCount">How many documents were indexed.</param>
/// <param name="Chunks">The chunks that were stored, with their embeddings.</param>
public sealed record IndexingResult(int DocumentCount, IReadOnlyList<Chunk> Chunks);
