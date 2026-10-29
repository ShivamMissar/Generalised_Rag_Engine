using Rag.Core.Models;

namespace Rag.Core.Storage;

/// <summary>
/// Safety checks shared by every <see cref="Abstractions.IVectorStore"/>, so all stores reject the same
/// mistakes with the same messages.
/// </summary>
/// <remarks>
/// The most important rule: <b>every vector in a collection must come from the same embedding model.</b>
/// Fingerprints from different models are like measurements in different units, so comparing them gives
/// meaningless scores. These checks catch that early with a clear error, instead of returning bad results.
/// </remarks>
public static class VectorStoreChecks
{
    /// <summary>Throws if an existing collection was created with a different model or vector size.</summary>
    public static void EnsureSameSettings(Collection existing, Collection requested)
    {
        if (existing.EmbeddingModel != requested.EmbeddingModel || existing.Dimensions != requested.Dimensions)
        {
            throw new InvalidOperationException(
                $"Collection '{requested.Name}' already exists with model '{existing.EmbeddingModel}' ({existing.Dimensions} dimensions), " +
                $"but '{requested.EmbeddingModel}' ({requested.Dimensions} dimensions) was requested. " +
                "Use a different collection name, or delete the existing one and re-index.");
        }
    }

    /// <summary>Throws if a chunk has no embedding, or one of the wrong size for the collection.</summary>
    public static void EnsureEmbeddingFits(Collection collection, Chunk chunk)
    {
        if (chunk.Embedding.IsEmpty)
        {
            throw new ArgumentException($"Chunk '{chunk.Id}' has no embedding. Embed chunks before storing them.");
        }
        if (chunk.Embedding.Length != collection.Dimensions)
        {
            throw new ArgumentException(
                $"Chunk '{chunk.Id}' has a {chunk.Embedding.Length}-dimension embedding, but collection '{collection.Name}' " +
                $"expects {collection.Dimensions}. Was it embedded with a different model?");
        }
    }

    /// <summary>Throws if a query embedding is the wrong size for the collection.</summary>
    public static void EnsureQueryFits(Collection collection, ReadOnlyMemory<float> queryEmbedding)
    {
        if (queryEmbedding.Length != collection.Dimensions)
        {
            throw new ArgumentException(
                $"The query embedding has {queryEmbedding.Length} dimensions, but collection '{collection.Name}' " +
                $"expects {collection.Dimensions}. Was the query embedded with a different model?");
        }
    }

    /// <summary>The error for searching or writing to a collection that hasn't been created.</summary>
    public static CollectionNotFoundException CollectionNotFound(string collectionName) => new(collectionName);
}
