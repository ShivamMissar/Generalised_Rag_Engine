using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Similarity;

namespace Rag.Core.Storage;

/// <summary>
/// An <see cref="IVectorStore"/> that keeps everything in memory, so nothing is saved when the program exits.
/// It's useful for tests and quick experiments, such as the eval runner comparing settings side by side,
/// and as the simplest possible example of what a vector store does. Use <c>SqliteVectorStore</c> to keep data.
/// </summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private sealed record Entry(Collection Collection, Dictionary<string, Chunk> Chunks);

    private readonly Dictionary<string, Entry> _collections = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public Task EnsureCollectionAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_collections.TryGetValue(collection.Name, out var existing))
            {
                VectorStoreChecks.EnsureSameSettings(existing.Collection, collection);
            }
            else
            {
                _collections[collection.Name] = new Entry(collection, new Dictionary<string, Chunk>(StringComparer.Ordinal));
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _collections.Remove(collectionName);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpsertAsync(string collectionName, IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var entry = GetEntry(collectionName);
            foreach (var chunk in chunks)
            {
                VectorStoreChecks.EnsureEmbeddingFits(entry.Collection, chunk);
                entry.Chunks[chunk.Id] = chunk;
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteDocumentsAsync(string collectionName, IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var entry = GetEntry(collectionName);
            var ids = documentIds.ToHashSet(StringComparer.Ordinal);
            foreach (var chunk in entry.Chunks.Values.Where(c => ids.Contains(c.DocumentId)).ToList())
            {
                entry.Chunks.Remove(chunk.Id);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var entry = GetEntry(collectionName);
            VectorStoreChecks.EnsureQueryFits(entry.Collection, queryEmbedding);
            return Task.FromResult(Ranking.TopK(entry.Chunks.Values, queryEmbedding, topK));
        }
    }

    private Entry GetEntry(string collectionName) =>
        _collections.TryGetValue(collectionName, out var entry)
            ? entry
            : throw VectorStoreChecks.CollectionNotFound(collectionName);
}
