using Rag.Core.Models;

namespace Rag.Core.Similarity;

/// <summary>
/// Picks the best-matching chunks for a query. This is the "search" in vector search.
/// </summary>
/// <remarks>
/// <b>In plain English:</b> score every chunk against the question with cosine similarity, then keep the
/// highest scorers. This is "brute force": it compares the question with every chunk. That's fast enough for
/// thousands of chunks (a CV and some write-ups have a few dozen). For millions you'd use an index that
/// only checks likely candidates, such as pgvector's, which is planned for later.
/// </remarks>
public static class Ranking
{
    /// <summary>
    /// Scores each candidate against <paramref name="queryEmbedding"/> and returns the <paramref name="topK"/>
    /// best, highest score first. Ties are broken by chunk ID so results are always in the same order.
    /// </summary>
    public static IReadOnlyList<SearchResult> TopK(IEnumerable<Chunk> candidates, ReadOnlyMemory<float> queryEmbedding, int topK)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);

        return candidates
            .Select(chunk => new SearchResult(chunk, VectorMath.CosineSimilarity(queryEmbedding.Span, chunk.Embedding.Span)))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Chunk.Id, StringComparer.Ordinal)
            .Take(topK)
            .ToList();
    }
}
