using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Evaluation;

/// <summary>
/// Runs every question in an <see cref="EvalSet"/> through a retriever and scores the results.
/// </summary>
/// <remarks>
/// It only tests the search half of RAG, not the AI's written answer. Search is the part that's easy to measure
/// automatically, and if search finds the wrong evidence, even a perfect AI gives a bad answer.
/// </remarks>
public sealed class RetrievalEvaluator(IRetriever retriever)
{
    /// <summary>Asks every question, keeps the top <paramref name="topK"/> results for each, and scores them.</summary>
    public async Task<EvalReport> RunAsync(EvalSet set, string collectionName, int topK = 3, CancellationToken cancellationToken = default)
    {
        var caseResults = new List<EvalCaseResult>();
        foreach (var evalCase in set.Cases)
        {
            var retrieved = await retriever.RetrieveAsync(evalCase.Question, collectionName, topK, cancellationToken);
            caseResults.Add(new EvalCaseResult(
                evalCase,
                retrieved,
                RetrievalMetrics.Recall(retrieved, evalCase.ExpectedSnippets),
                RetrievalMetrics.ReciprocalRank(retrieved, evalCase.ExpectedSnippets)));
        }
        return new EvalReport(topK, caseResults);
    }
}

/// <summary>The scores for one question.</summary>
public sealed record EvalCaseResult(EvalCase Case, IReadOnlyList<SearchResult> Retrieved, double Recall, double ReciprocalRank);

/// <summary>The scores for a whole eval set, with averages.</summary>
/// <param name="TopK">How many results were kept per question.</param>
/// <param name="Cases">Per-question results.</param>
public sealed record EvalReport(int TopK, IReadOnlyList<EvalCaseResult> Cases)
{
    /// <summary>Average recall@k: on average, how much of what we wanted was found.</summary>
    public double MeanRecall => Cases.Count == 0 ? 0 : Cases.Average(c => c.Recall);

    /// <summary>MRR: on average, how near the top the first good result was.</summary>
    public double MeanReciprocalRank => Cases.Count == 0 ? 0 : Cases.Average(c => c.ReciprocalRank);

    /// <summary>Hit rate@k: the fraction of questions with at least one relevant result.</summary>
    public double HitRate => Cases.Count == 0 ? 0 : Cases.Average(c => c.ReciprocalRank > 0 ? 1.0 : 0.0);
}
