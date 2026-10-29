using System.ClientModel;
using Microsoft.AspNetCore.Diagnostics;
using Rag.Api.Protection;
using Rag.Api.Sessions;
using Rag.Core.Storage;
using Rag.Documents;

namespace Rag.Api;

/// <summary>
/// Turns expected failures into helpful error responses, instead of a generic "500 Internal Server Error". Responses
/// use the standard "problem details" JSON format, and every message is written to be shown to a visitor.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>File can't be read</b> → 400, with the reason (wrong type, scanned PDF, damaged).</item>
/// <item><b>Upload too large</b> → 413.</item>
/// <item><b>Session expired or unknown</b> → 404: "upload your CV again".</item>
/// <item><b>Daily allowance used up</b> → 429.</item>
/// <item><b>Server at capacity, or the AI provider busy or unreachable</b> → 503: "try again shortly".</item>
/// <item><b>AI provider rejected the request</b> (e.g. a bad API key) → 502, with the details only in the server log.</item>
/// </list>
/// Anything else falls through to the default handler (a plain 500, with details only in Development).
/// </remarks>
public sealed class RagExceptionHandler(IProblemDetailsService problemDetails, ILogger<RagExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Title, string Detail)? problem = exception switch
        {
            DocumentParseException e => (StatusCodes.Status400BadRequest, "Couldn't read your file", e.Message),
            InvalidDataException or BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge, "Upload too large", "Your files are too large. Try smaller or fewer files."),
            SessionNotFoundException e => (StatusCodes.Status404NotFound, "Session expired", e.Message),
            // The session was deleted while a request was using it.
            CollectionNotFoundException => (StatusCodes.Status404NotFound, "Session expired", new SessionNotFoundException().Message),
            UsageLimitReachedException e => (StatusCodes.Status429TooManyRequests, "Daily limit reached", e.Message),
            ServiceBusyException e => (StatusCodes.Status503ServiceUnavailable, "Busy right now", e.Message),
            ClientResultException { Status: 429 or >= 500 } => (StatusCodes.Status503ServiceUnavailable, "The AI service is busy",
                "The AI service is busy or has hit its free usage limit for now. Please try again in a minute."),
            ClientResultException => (StatusCodes.Status502BadGateway, "The AI service rejected the request",
                "Something went wrong talking to the AI service. Please try again later."),
            HttpRequestException => (StatusCodes.Status503ServiceUnavailable, "The AI models are unavailable",
                "Couldn't reach the AI service. If you're running locally, check Ollama is running: brew services start ollama"),
            _ => null,
        };

        if (problem is not { } p)
        {
            return false;
        }

        // Rejections by the AI provider usually mean a configuration problem (like a wrong API key), so log them as
        // errors. Everything else here is an expected, visitor-caused condition.
        if (exception is ClientResultException { Status: not (429 or >= 500) })
        {
            logger.LogError(exception, "The AI provider rejected a request. Check the API key and model names.");
        }
        else
        {
            logger.LogWarning("{Title}: {Message}", p.Title, exception.Message);
        }

        httpContext.Response.StatusCode = p.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = p.Status, Title = p.Title, Detail = p.Detail },
        });
    }
}
