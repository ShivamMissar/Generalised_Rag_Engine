using Rag.Core.Models;

namespace Rag.Core.Abstractions;

/// <summary>
/// Persists embedded <see cref="Chunk"/>s, grouped into <see cref="Collection"/>s, and finds the chunks
/// whose embeddings are nearest to a query vector. The storage backend sits behind this interface:
/// SQLite first, pgvector later.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Creates <paramref name="collection"/> if it doesn't already exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">A collection with this name exists but uses a different embedding model or dimensions.</exception>
    Task EnsureCollectionAsync(Collection collection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or replaces embedded chunks in the named collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection doesn't exist.</exception>
    /// <exception cref="ArgumentException">A chunk has no embedding, or one of the wrong length.</exception>
    Task UpsertAsync(string collectionName, IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the named collection and all of its chunks. Does nothing if it doesn't exist. Used to remove a
    /// visitor's uploaded documents when they ask, or when their session expires.
    /// </summary>
    Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every chunk belonging to the given documents from the named collection. Used before
    /// re-indexing a document, so chunks left over from an older, longer version don't linger.
    /// </summary>
    Task DeleteDocumentsAsync(string collectionName, IEnumerable<string> documentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="topK"/> chunks from the named collection that are most similar to
    /// <paramref name="queryEmbedding"/>, ordered from most to least relevant.
    /// </summary>
    /// <exception cref="InvalidOperationException">The collection doesn't exist.</exception>
    /// <exception cref="ArgumentException">The query embedding has the wrong length.</exception>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default);
}
