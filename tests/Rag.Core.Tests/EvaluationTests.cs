using Rag.Core.Abstractions;
using Rag.Core.Chunking;
using Rag.Core.Evaluation;
using Rag.Core.Loading;
using Rag.Core.Models;

namespace Rag.Core.Tests;

public class EvaluationTests
{
    private static IReadOnlyList<SearchResult> Results(params string[] contents) =>
        contents.Select((content, i) => TestData.Result($"c{i}", 1.0 - i * 0.1, content)).ToList();

    [Fact]
    public void Recall_is_the_fraction_of_snippets_found()
    {
        var results = Results("uses Redis caching", "unrelated");

        Assert.Equal(1.0, RetrievalMetrics.Recall(results, ["redis"]));          // case doesn't matter
        Assert.Equal(0.5, RetrievalMetrics.Recall(results, ["Redis", "Kafka"]));
        Assert.Equal(0.0, RetrievalMetrics.Recall(results, ["Kafka"]));
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 0.5)]
    [InlineData(2, 1.0 / 3)]
    public void Reciprocal_rank_is_one_over_the_position_of_the_first_relevant_result(int position, double expected)
    {
        var contents = new[] { "a", "b", "c" };
        contents[position] = "the answer";

        Assert.Equal(expected, RetrievalMetrics.ReciprocalRank(Results(contents), ["answer"]), precision: 10);
    }

    [Fact]
    public void Reciprocal_rank_is_zero_when_nothing_is_relevant()
    {
        Assert.Equal(0, RetrievalMetrics.ReciprocalRank(Results("a", "b"), ["answer"]));
    }

    [Fact]
    public async Task Evaluator_scores_each_question_and_averages_them()
    {
        var retriever = new FixedRetriever(Results("found it", "other"));
        var set = new EvalSet(null,
        [
            new EvalCase("q1", ["found it"]),
            new EvalCase("q2", ["missing"]),
        ]);

        var report = await new RetrievalEvaluator(retriever).RunAsync(set, "c", topK: 2);

        Assert.Equal([1.0, 0.0], report.Cases.Select(c => c.Recall));
        Assert.Equal(0.5, report.MeanRecall);
        Assert.Equal(0.5, report.MeanReciprocalRank);
        Assert.Equal(0.5, report.HitRate);
    }

    /// <summary>
    /// Guards the real eval set: it must load, and every expected snippet must really exist in some chunk of the
    /// sample documents. Otherwise a typo would make a question impossible to pass.
    /// </summary>
    [Fact]
    public async Task Every_snippet_in_the_eval_set_exists_in_the_sample_documents()
    {
        var root = FindRepoRoot();
        var set = await EvalSet.LoadAsync(Path.Combine(root, "evals", "retrieval.json"));
        var documents = await new TextFileLoader().LoadAsync(Path.Combine(root, "samples", "candidate"));
        var chunker = new MarkdownSectionChunker();
        var chunks = documents.SelectMany(d => chunker.Chunk(d)).ToList();

        var missing = set.Cases
            .SelectMany(c => c.ExpectedSnippets)
            .Where(snippet => !chunks.Any(chunk => RetrievalMetrics.ContainsSnippet(chunk, snippet)))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public async Task An_eval_file_without_cases_is_rejected()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """{ "cases": [] }""");
            await Assert.ThrowsAsync<InvalidDataException>(() => EvalSet.LoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("*.slnx").Any())
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("No .slnx file found above the test output folder.");
    }

    private sealed class FixedRetriever(IReadOnlyList<SearchResult> results) : IRetriever
    {
        public Task<IReadOnlyList<SearchResult>> RetrieveAsync(string query, string collectionName, int topK = 5, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchResult>>(results.Take(topK).ToList());
    }
}
