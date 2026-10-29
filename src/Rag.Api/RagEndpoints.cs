using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Indexing;
using Rag.Core.JobMatching;
using Rag.Core.Models;

namespace Rag.Api;

/// <summary>
/// The API's endpoints: the "front counter" where other programs (like the React front end) use the engine.
/// </summary>
/// <remarks>
/// Each endpoint is a thin wrapper: check the input, call the engine, and turn the result into JSON. All the real
/// work happens in Rag.Core. Errors such as "nothing indexed yet" or "Ollama isn't running" are turned into clear
/// responses by <see cref="RagExceptionHandler"/>.
/// </remarks>
public static class RagEndpoints
{
    /// <summary>The most results a search can ask for.</summary>
    public const int MaxTopK = 20;

    public static IEndpointRouteBuilder MapRagEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/index", IndexAsync)
            .WithSummary("Rebuild the index from the documents folder")
            .WithDescription("Loads, chunks and fingerprints every .txt and .md file in the configured folder. Takes a few seconds.");

        api.MapGet("/search", SearchAsync)
            .WithSummary("Find the chunks that best match a question");

        api.MapPost("/match", MatchAsync)
            .WithSummary("Assess how well the indexed documents match a job ad")
            .WithDescription("Returns the requirements found, the evidence given to the AI, and its report. Can take up to a minute.");

        return app;
    }

    private static async Task<Ok<IndexResponse>> IndexAsync(
        IDocumentLoader loader,
        DocumentIndexer indexer,
        Collection collection,
        IOptions<RagOptions> options,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var documentsPath = Path.GetFullPath(options.Value.DocumentsPath, environment.ContentRootPath);
        var documents = await loader.LoadAsync(documentsPath, cancellationToken);
        var result = await indexer.IndexAsync(collection, documents, cancellationToken);

        return TypedResults.Ok(new IndexResponse(collection.Name, result.DocumentCount, result.Chunks.Count));
    }

    private static async Task<Results<Ok<SearchResponse>, ValidationProblem>> SearchAsync(
        string? q,
        int? topK,
        IRetriever retriever,
        Collection collection,
        CancellationToken cancellationToken)
    {
        var k = topK ?? 5;
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(q))
        {
            errors["q"] = ["Provide a question, e.g. /api/search?q=cloud+experience"];
        }
        if (k is < 1 or > MaxTopK)
        {
            errors["topK"] = [$"topK must be between 1 and {MaxTopK}."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var results = await retriever.RetrieveAsync(q!, collection.Name, k, cancellationToken);
        return TypedResults.Ok(new SearchResponse(q!, results.Select(ChunkHit.From).ToList()));
    }

    private static async Task<Results<Ok<MatchResponse>, ValidationProblem>> MatchAsync(
        MatchRequest request,
        JobMatchAnalyzer analyzer,
        Collection collection,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JobAd))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["jobAd"] = ["Provide the job ad text."],
            });
        }

        var result = await analyzer.AnalyzeAsync(request.JobAd, collection.Name, cancellationToken);
        return TypedResults.Ok(new MatchResponse(
            result.Requirements,
            result.Evidence.Select(ChunkHit.From).ToList(),
            result.Report));
    }
}
