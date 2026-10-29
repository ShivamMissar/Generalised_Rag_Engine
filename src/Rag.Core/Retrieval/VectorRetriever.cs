using Microsoft.Extensions.AI;
using Rag.Core.Abstractions;
using Rag.Core.Indexing;
using Rag.Core.Models;

namespace Rag.Core.Retrieval;

/// <summary>
/// Finds the chunks that best match a question, by fingerprinting the question and searching the vector store.
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b> this is "asking the librarian". The question gets its own fingerprint, using the
/// same model as the documents, and the store returns the chunks whose fingerprints point in the most similar
/// direction (highest cosine similarity).</para>
/// <code>
/// question → embedding model → question fingerprint → vector store search → top-k chunks
/// </code>
/// <para>There's no minimum score. Scores are only meaningful compared with each other (unrelated text still
/// scored around 0.3 in our experiments), so we take the top <c>k</c> rather than "everything above 0.6".</para>
/// </remarks>
public sealed class VectorRetriever(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IVectorStore store,
    EmbeddingPrefixes prefixes) : IRetriever
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        string collectionName,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        // The question gets the "query" label, matching the "document" label the chunks got when indexed.
        var queryEmbedding = await embedder.GenerateVectorAsync(prefixes.Query + query, cancellationToken: cancellationToken);
        return await store.SearchAsync(collectionName, queryEmbedding, topK, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>Embeds every query in a single request to the embedding model, then searches once per query.</remarks>
    public async Task<IReadOnlyList<IReadOnlyList<SearchResult>>> RetrieveManyAsync(
        IReadOnlyList<string> queries,
        string collectionName,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        foreach (var query in queries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
        }
        if (queries.Count == 0)
        {
            return [];
        }

        var embeddings = await embedder.GenerateAsync(queries.Select(query => prefixes.Query + query), cancellationToken: cancellationToken);
        if (embeddings.Count != queries.Count)
        {
            throw new InvalidOperationException($"Asked for {queries.Count} embeddings but got {embeddings.Count}.");
        }

        var results = new List<IReadOnlyList<SearchResult>>(queries.Count);
        foreach (var embedding in embeddings)
        {
            results.Add(await store.SearchAsync(collectionName, embedding.Vector, topK, cancellationToken));
        }
        return results;
    }
}
