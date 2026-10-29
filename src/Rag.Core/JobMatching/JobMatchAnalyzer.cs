using Microsoft.Extensions.AI;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.JobMatching;

/// <summary>
/// The job-application assistant: compares a job ad with the candidate's documents and asks the chat model
/// for a match report backed by cited evidence. This is the full RAG loop (retrieve, then generate).
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b></para>
/// <list type="number">
/// <item>Split the job ad into requirements (<see cref="JobAdParser"/>).</item>
/// <item>For each requirement, find the best-matching chunks of the CV and write-ups (the retriever).</item>
/// <item>Combine those into one evidence list, without duplicates (see <see cref="SelectEvidence"/>).</item>
/// <item>Build the prompt (<see cref="JobMatchPrompt"/>) and ask the chat model for the report.</item>
/// </list>
/// </remarks>
public sealed class JobMatchAnalyzer(IRetriever retriever, IChatClient chat, JobMatchOptions? options = null)
{
    private readonly JobMatchOptions _options = options ?? new JobMatchOptions();

    /// <summary>Analyses how well the documents in <paramref name="collectionName"/> match <paramref name="jobAd"/>.</summary>
    public async Task<JobMatchResult> AnalyzeAsync(string jobAd, string collectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobAd);

        // 1. Requirements. If none could be found, search with the whole ad instead.
        var requirements = JobAdParser.ExtractRequirements(jobAd);
        var queries = requirements.Count > 0 ? requirements : [jobAd];

        // 2. Retrieve candidates for each requirement (in one batch, so it's one embedding request, not one each).
        var resultsPerQuery = await retriever.RetrieveManyAsync(queries, collectionName, _options.ChunksPerRequirement, cancellationToken);

        // 3. Merge into one evidence list.
        var evidence = SelectEvidence(resultsPerQuery, _options.MaxEvidence);

        // 4. Ask the chat model. A low temperature makes answers more focused and consistent between runs.
        var messages = JobMatchPrompt.Build(jobAd, queries, evidence);
        var response = await chat.GetResponseAsync(messages, new ChatOptions { Temperature = _options.Temperature }, cancellationToken);

        return new JobMatchResult(queries, evidence, response.Text);
    }

    /// <summary>
    /// Merges each requirement's search results into one list of at most <paramref name="maxEvidence"/> chunks,
    /// with no duplicates, ordered by score.
    /// </summary>
    /// <remarks>
    /// <para>The same chunk often matches several requirements (a skills list matches lots of things), so results
    /// are de-duplicated, keeping each chunk's best score.</para>
    /// <para>Choosing only the highest scores overall could give all the slots to the easiest requirements. So it
    /// works in two passes:</para>
    /// <list type="number">
    /// <item><b>Fair share:</b> first, take the single best chunk for each requirement, so every requirement gets
    /// some evidence.</item>
    /// <item><b>Fill up:</b> then fill any remaining slots with the highest-scoring chunks overall.</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<SearchResult> SelectEvidence(IReadOnlyList<IReadOnlyList<SearchResult>> resultsPerQuery, int maxEvidence)
    {
        // Each chunk's best score across all requirements.
        var bestById = new Dictionary<string, SearchResult>(StringComparer.Ordinal);
        foreach (var result in resultsPerQuery.SelectMany(results => results))
        {
            if (!bestById.TryGetValue(result.Chunk.Id, out var existing) || result.Score > existing.Score)
            {
                bestById[result.Chunk.Id] = result;
            }
        }

        var selectedIds = new HashSet<string>(StringComparer.Ordinal);

        // Pass 1: the top chunk for each requirement.
        foreach (var results in resultsPerQuery)
        {
            if (selectedIds.Count == maxEvidence)
            {
                break;
            }
            if (results.Count > 0)
            {
                selectedIds.Add(results[0].Chunk.Id);
            }
        }

        // Pass 2: fill the remaining slots by best score.
        var byScore = bestById.Values
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Chunk.Id, StringComparer.Ordinal);
        foreach (var result in byScore)
        {
            if (selectedIds.Count == maxEvidence)
            {
                break;
            }
            selectedIds.Add(result.Chunk.Id);
        }

        return byScore.Where(result => selectedIds.Contains(result.Chunk.Id)).ToList();
    }
}

/// <summary>Settings for <see cref="JobMatchAnalyzer"/>.</summary>
/// <param name="ChunksPerRequirement">How many chunks to retrieve for each requirement.</param>
/// <param name="MaxEvidence">The most chunks to include in the prompt. More gives the AI more to go on, but a longer prompt is slower and can distract it.</param>
/// <param name="Temperature">How much randomness the chat model uses. Low values (like 0.2) give focused, repeatable answers.</param>
public sealed record JobMatchOptions(int ChunksPerRequirement = 3, int MaxEvidence = 8, float Temperature = 0.2f);

/// <summary>The outcome of a job-match analysis.</summary>
/// <param name="Requirements">The requirements searched for (or the whole ad, if none were found).</param>
/// <param name="Evidence">The chunks given to the AI, in the order they were numbered in the prompt.</param>
/// <param name="Report">The chat model's match report.</param>
public sealed record JobMatchResult(IReadOnlyList<string> Requirements, IReadOnlyList<SearchResult> Evidence, string Report);
