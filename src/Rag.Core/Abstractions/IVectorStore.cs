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
    Task EnsureCollectionAsync(Collection collection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or replaces embedded chunks in the named collection.
    /// </summary>
    Task UpsertAsync(string collectionName, IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="topK"/> chunks from the named collection that are most similar to
    /// <paramref name="queryEmbedding"/>, ordered from most to least relevant.
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default);
}
