using Microsoft.Extensions.AI;
using Rag.Core.Abstractions;
using Rag.Core.JobMatching;
using Rag.Core.Models;
using Rag.Core.Tests.Fakes;

namespace Rag.Core.Tests;

public class JobMatchingTests
{
    [Fact]
    public void Requirements_are_the_ads_bullet_points()
    {
        var ad = """
            # Backend Developer
            We're hiring.

            ## You'll need
            - C# and .NET
            * SQL skills
            1. Automated testing
            """;

        Assert.Equal(["C# and .NET", "SQL skills", "Automated testing"], JobAdParser.ExtractRequirements(ad));
    }

    [Fact]
    public void Without_bullets_requirements_are_the_longer_sentences()
    {
        var ad = "# Job\nWe need strong experience with C# and .NET. You should know SQL databases well. Apply now!";

        Assert.Equal(
            ["We need strong experience with C# and .NET.", "You should know SQL databases well."],
            JobAdParser.ExtractRequirements(ad));
    }

    [Fact]
    public void At_most_the_maximum_number_of_requirements_are_extracted()
    {
        var ad = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"- Requirement {i}"));

        Assert.Equal(JobAdParser.MaxRequirements, JobAdParser.ExtractRequirements(ad).Count);
    }

    [Fact]
    public void Evidence_gives_every_requirement_its_best_chunk_before_filling_by_score()
    {
        // Requirement 1 has many strong matches, requirement 2 only a weak one.
        IReadOnlyList<SearchResult> strong = [TestData.Result("s1", 0.9), TestData.Result("s2", 0.85), TestData.Result("s3", 0.8)];
        IReadOnlyList<SearchResult> weak = [TestData.Result("w1", 0.4)];

        var evidence = JobMatchAnalyzer.SelectEvidence([strong, weak], maxEvidence: 2);

        // Picking purely by score would give s1 and s2, leaving requirement 2 with nothing.
        Assert.Equal(["s1", "w1"], evidence.Select(r => r.Chunk.Id));
    }

    [Fact]
    public void Evidence_has_no_duplicates_and_keeps_each_chunks_best_score()
    {
        IReadOnlyList<SearchResult> first = [TestData.Result("a", 0.5), TestData.Result("b", 0.4)];
        IReadOnlyList<SearchResult> second = [TestData.Result("a", 0.7)];

        var evidence = JobMatchAnalyzer.SelectEvidence([first, second], maxEvidence: 5);

        Assert.Equal(["a", "b"], evidence.Select(r => r.Chunk.Id));
        Assert.Equal(0.7, evidence[0].Score);
    }

    [Fact]
    public async Task Analyzer_searches_each_requirement_and_sends_cited_evidence_to_the_model()
    {
        var retriever = new FakeRetriever(query => query.Contains("SQL")
            ? [TestData.Result("cv.md#2", 0.8, "Designed PostgreSQL schemas")]
            : [TestData.Result("cv.md#1", 0.7, "Built REST APIs in C#")]);
        var chat = new RecordingChatClient("Overall match: Strong.");
        var analyzer = new JobMatchAnalyzer(retriever, chat);

        var result = await analyzer.AnalyzeAsync("## Needs\n- C# APIs\n- SQL databases", "cv");

        Assert.Equal(["C# APIs", "SQL databases"], retriever.Queries);
        Assert.Equal("Overall match: Strong.", result.Report);
        Assert.Equal(["cv.md#2", "cv.md#1"], result.Evidence.Select(r => r.Chunk.Id));

        Assert.Equal(ChatRole.System, chat.LastMessages[0].Role);
        var userPrompt = chat.LastMessages[1].Text;
        Assert.Contains("[1] (from doc)\nDesigned PostgreSQL schemas", userPrompt);
        Assert.Contains("[2] (from doc)\nBuilt REST APIs in C#", userPrompt);
        Assert.Contains("1. C# APIs", userPrompt);
        Assert.Equal(0.2f, chat.LastOptions?.Temperature);
    }

    [Fact]
    public async Task Analyzer_searches_with_the_whole_ad_when_it_has_no_requirements()
    {
        var retriever = new FakeRetriever(_ => []);
        var analyzer = new JobMatchAnalyzer(retriever, new RecordingChatClient("ok"));

        await analyzer.AnalyzeAsync("Hiring!", "cv");

        Assert.Equal(["Hiring!"], retriever.Queries);
    }

    private sealed class FakeRetriever(Func<string, IReadOnlyList<SearchResult>> results) : IRetriever
    {
        public List<string> Queries { get; } = [];

        public Task<IReadOnlyList<SearchResult>> RetrieveAsync(string query, string collectionName, int topK = 5, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(results(query));
        }
    }
}
