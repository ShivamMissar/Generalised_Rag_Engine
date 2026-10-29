using Rag.Core.Models;

namespace Rag.Core.Evaluation;

/// <summary>
/// Scores for how good one search was, given what it should have found.
/// </summary>
/// <remarks>
/// <para>Two standard measures, both from 0 (bad) to 1 (perfect):</para>
/// <list type="bullet">
/// <item><b>Recall</b>: "did it find everything?" The fraction of expected snippets that appear anywhere in the
/// results. If a question expects 2 snippets and the results contain 1, recall is 0.5. When measured over the top
/// k results, it's called "recall@k".</item>
/// <item><b>Reciprocal rank</b>: "how high up was the first good result?" It's 1 if the first result is relevant,
/// 1/2 if the second is, 1/3 for the third, and 0 if none are. Averaged over all questions, it's called MRR (mean
/// reciprocal rank). It rewards putting the right answer at the top, which matters because the AI pays most
/// attention to the best evidence.</item>
/// </list>
/// </remarks>
public static class RetrievalMetrics
{
    /// <summary>True if the chunk contains the snippet, ignoring upper/lower case.</summary>
    public static bool ContainsSnippet(Chunk chunk, string snippet) =>
        chunk.Content.Contains(snippet, StringComparison.OrdinalIgnoreCase);

    /// <summary>True if the chunk contains any of the expected snippets.</summary>
    public static bool IsRelevant(Chunk chunk, IReadOnlyList<string> expectedSnippets) =>
        expectedSnippets.Any(snippet => ContainsSnippet(chunk, snippet));

    /// <summary>The fraction of <paramref name="expectedSnippets"/> found anywhere in <paramref name="results"/>.</summary>
    public static double Recall(IReadOnlyList<SearchResult> results, IReadOnlyList<string> expectedSnippets)
    {
        if (expectedSnippets.Count == 0)
        {
            throw new ArgumentException("At least one expected snippet is needed.", nameof(expectedSnippets));
        }

        var found = expectedSnippets.Count(snippet => results.Any(result => ContainsSnippet(result.Chunk, snippet)));
        return (double)found / expectedSnippets.Count;
    }

    /// <summary>1 divided by the position of the first relevant result (1 for first place, 0.5 for second...), or 0 if none is relevant.</summary>
    public static double ReciprocalRank(IReadOnlyList<SearchResult> results, IReadOnlyList<string> expectedSnippets)
    {
        for (var i = 0; i < results.Count; i++)
        {
            if (IsRelevant(results[i].Chunk, expectedSnippets))
            {
                return 1.0 / (i + 1);
            }
        }
        return 0;
    }
}
