namespace Rag.Core.Similarity;

/// <summary>
/// Measures for comparing embedding vectors. Retrieval uses these to rank chunks by how close their
/// embeddings are to a query's embedding.
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Cosine similarity of two vectors: the cosine of the angle between them. Only direction counts,
    /// not length. 1 means the same direction, 0 unrelated (perpendicular), -1 opposite.
    /// </summary>
    /// <exception cref="ArgumentException">The vectors differ in length, or either is all zeros.</exception>
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException($"Vectors must be the same length, but got {a.Length} and {b.Length}.");
        }

        float dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            throw new ArgumentException("Vectors must not be zero vectors.");
        }
        return dot / MathF.Sqrt(normA * normB);
    }
}
