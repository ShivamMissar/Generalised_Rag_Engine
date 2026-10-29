using Microsoft.AspNetCore.Diagnostics;
using Rag.Core.Storage;

namespace Rag.Api;

/// <summary>
/// Turns the engine's expected failures into helpful error responses, instead of a generic "500 Internal Server
/// Error". Responses use the standard "problem details" JSON format, which front ends know how to read.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Nothing indexed yet</b> → 409 Conflict: "call POST /api/index first".</item>
/// <item><b>Ollama unreachable</b> → 503 Service Unavailable: the AI models aren't running.</item>
/// <item><b>Documents folder missing</b> → 500 with the path, so the setting can be fixed.</item>
/// </list>
/// Anything else falls through to the default handler (a plain 500, with details only in Development).
/// </remarks>
public sealed class RagExceptionHandler(IProblemDetailsService problemDetails, ILogger<RagExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Title, string Detail)? problem = exception switch
        {
            CollectionNotFoundException e => (StatusCodes.Status409Conflict, "Nothing has been indexed yet",
                $"{e.Message} Call POST /api/index first."),
            HttpRequestException e => (StatusCodes.Status503ServiceUnavailable, "The AI models are unavailable",
                $"Couldn't reach Ollama ({e.Message}). Check it's running: brew services start ollama"),
            FileNotFoundException e => (StatusCodes.Status500InternalServerError, "Documents folder not found",
                $"{e.Message} Check Rag:DocumentsPath in appsettings.json."),
            _ => null,
        };

        if (problem is not { } p)
        {
            return false;
        }

        logger.LogWarning(exception, "{Title}", p.Title);
        httpContext.Response.StatusCode = p.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = p.Status, Title = p.Title, Detail = p.Detail },
        });
    }
}
