using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Rag.Api.Protection;
using Rag.Api.Sessions;
using Rag.Core.Abstractions;
using Rag.Core.JobMatching;
using Rag.Core.Models;
using Rag.Documents;

namespace Rag.Api;

/// <summary>
/// The API's endpoints: the "front counter" where the web page uses the engine.
/// </summary>
/// <remarks>
/// <para>The flow for a visitor:</para>
/// <list type="number">
/// <item><c>POST /api/sessions</c> uploads their CV (or <c>POST /api/sessions/sample</c> uses the sample one) and
/// returns a private session ID.</item>
/// <item><c>POST /api/match</c> and <c>GET /api/search</c> use that session ID to search only their documents.</item>
/// <item><c>DELETE /api/sessions/{id}</c> removes their documents straight away. Otherwise they're deleted
/// automatically when the session expires.</item>
/// </list>
/// <para>Each endpoint is a thin wrapper: check the input, call the engine, turn the result into JSON. Errors are
/// turned into clear messages by <see cref="RagExceptionHandler"/>.</para>
/// </remarks>
public static class RagEndpoints
{
    /// <summary>The most results a search can ask for.</summary>
    public const int MaxTopK = 20;

    public static IEndpointRouteBuilder MapRagEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/info", GetInfo)
            .WithSummary("Which AI provider is in use, and the upload and usage limits");

        api.MapPost("/sessions", CreateSessionAsync)
            .WithSummary("Upload a CV and other documents (PDF, Word, Markdown or text) into a private session")
            .RequireRateLimiting(ProtectionExtensions.UploadsPolicy);

        api.MapPost("/sessions/sample", CreateSampleSessionAsync)
            .WithSummary("Use the built-in sample CV instead of uploading one")
            .RequireRateLimiting(ProtectionExtensions.UploadsPolicy);

        api.MapGet("/sessions/{id}", GetSession)
            .WithSummary("Check a session still exists, e.g. after the page reloads");

        api.MapDelete("/sessions/{id}", DeleteSessionAsync)
            .WithSummary("Delete a session and its documents now");

        api.MapGet("/search", SearchAsync)
            .WithSummary("Find the chunks of a session's documents that best match a question")
            .RequireRateLimiting(ProtectionExtensions.SearchesPolicy);

        api.MapPost("/match", MatchAsync)
            .WithSummary("Assess how well a session's documents match a job ad")
            .WithDescription("Returns the requirements found, the evidence given to the AI, and its report. Can take up to a minute.")
            .RequireRateLimiting(ProtectionExtensions.MatchesPolicy);

        return app;
    }

    private static Ok<InfoResponse> GetInfo(IOptions<RagOptions> options, DailyUsageBudget budget)
    {
        var o = options.Value;
        return TypedResults.Ok(new InfoResponse(
            o.IsGemini ? "Google Gemini" : "Local models (Ollama)",
            o.IsGemini ? o.Gemini.ChatModel : o.Ollama.ChatModel,
            o.Limits.SessionMinutes,
            o.Limits.MaxFiles,
            Math.Round(o.Limits.MaxFileBytes / (1024.0 * 1024.0), 1),
            DocumentParser.SupportedExtensions.Order().ToList(),
            budget.Remaining));
    }

    private static async Task<Results<Ok<SessionResponse>, ValidationProblem>> CreateSessionAsync(
        HttpRequest request,
        SessionService sessionService,
        IOptions<RagOptions> options,
        CancellationToken cancellationToken)
    {
        var limits = options.Value.Limits;
        if (!request.HasFormContentType)
        {
            return Invalid("files", "Upload your documents as files (multipart form data).");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files.Count == 0)
        {
            return Invalid("files", "Choose at least one file to upload.");
        }
        if (form.Files.Count > limits.MaxFiles)
        {
            return Invalid("files", $"Upload at most {limits.MaxFiles} files.");
        }

        var documents = new List<Document>();
        foreach (var file in form.Files)
        {
            if (file.Length > limits.MaxFileBytes)
            {
                return Invalid("files", $"'{Path.GetFileName(file.FileName)}' is larger than {limits.MaxFileBytes / (1024 * 1024)} MB.");
            }
            await using var stream = file.OpenReadStream();
            var document = await DocumentParser.ParseAsync(file.FileName, stream, cancellationToken);
            documents.Add(WithUniqueName(document, documents));
        }

        var characters = documents.Sum(d => d.Content.Length);
        if (characters > limits.MaxDocumentChars)
        {
            return Invalid("files",
                $"Your documents contain {characters:N0} characters of text, and the limit is {limits.MaxDocumentChars:N0}. " +
                "Try a shorter CV or fewer files.");
        }

        var session = await sessionService.CreateAsync(documents, cancellationToken);
        return TypedResults.Ok(SessionResponse.From(session));
    }

    private static async Task<Ok<SessionResponse>> CreateSampleSessionAsync(SessionService sessionService, CancellationToken cancellationToken) =>
        TypedResults.Ok(SessionResponse.From(await sessionService.GetOrCreateSampleAsync(cancellationToken)));

    private static Ok<SessionResponse> GetSession(string id, SessionStore sessions) =>
        TypedResults.Ok(SessionResponse.From(sessions.Get(id)));

    private static async Task<NoContent> DeleteSessionAsync(string id, SessionService sessionService, CancellationToken cancellationToken)
    {
        await sessionService.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<SearchResponse>, ValidationProblem>> SearchAsync(
        string? q,
        int? topK,
        string? sessionId,
        IRetriever retriever,
        SessionStore sessions,
        DailyUsageBudget budget,
        CancellationToken cancellationToken)
    {
        var k = topK ?? 5;
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(q))
        {
            errors["q"] = ["Type a question to search for."];
        }
        if (k is < 1 or > MaxTopK)
        {
            errors["topK"] = [$"topK must be between 1 and {MaxTopK}."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var session = sessions.Get(sessionId);
        budget.Consume();
        var results = await retriever.RetrieveAsync(q!, session.CollectionName, k, cancellationToken);
        return TypedResults.Ok(new SearchResponse(q!, results.Select(ChunkHit.From).ToList()));
    }

    private static async Task<Results<Ok<MatchResponse>, ValidationProblem>> MatchAsync(
        MatchRequest request,
        JobMatchAnalyzer analyzer,
        SessionStore sessions,
        DailyUsageBudget budget,
        IOptions<RagOptions> options,
        CancellationToken cancellationToken)
    {
        var maxChars = options.Value.Limits.MaxJobAdChars;
        if (string.IsNullOrWhiteSpace(request.JobAd))
        {
            return Invalid("jobAd", "Paste the job ad first.");
        }
        if (request.JobAd.Length > maxChars)
        {
            return Invalid("jobAd", $"The job ad is {request.JobAd.Length:N0} characters long, and the limit is {maxChars:N0}.");
        }

        var session = sessions.Get(request.SessionId);
        budget.Consume();
        var result = await analyzer.AnalyzeAsync(request.JobAd, session.CollectionName, cancellationToken);
        return TypedResults.Ok(new MatchResponse(
            result.Requirements,
            result.Evidence.Select(ChunkHit.From).ToList(),
            result.Report));
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>Two uploads called "cv.pdf" would overwrite each other's chunks, so the second becomes "cv (2).pdf".</summary>
    private static Document WithUniqueName(Document document, IReadOnlyList<Document> existing)
    {
        var name = document.Id;
        for (var n = 2; existing.Any(d => d.Id.Equals(name, StringComparison.OrdinalIgnoreCase)); n++)
        {
            name = $"{Path.GetFileNameWithoutExtension(document.Id)} ({n}){Path.GetExtension(document.Id)}";
        }
        return name == document.Id
            ? document
            : document with { Id = name, Metadata = new Dictionary<string, string>(document.Metadata) { ["fileName"] = name } };
    }
}
