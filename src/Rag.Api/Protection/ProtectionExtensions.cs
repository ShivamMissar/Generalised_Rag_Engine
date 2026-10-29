using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Rag.Api.Protection;

/// <summary>
/// Per-visitor rate limits, upload size limits, and reading the visitor's real IP address behind the host's proxy.
/// </summary>
public static class ProtectionExtensions
{
    public const string UploadsPolicy = "uploads";
    public const string SearchesPolicy = "searches";
    public const string MatchesPolicy = "matches";

    public static IServiceCollection AddRagProtection(this IServiceCollection services)
    {
        // On a host like Render, requests reach the app through a proxy, so the visitor's address arrives in the
        // X-Forwarded-For header. Trusting it lets rate limits work per visitor. The header can be faked, which is
        // why the site-wide DailyUsageBudget exists as a backstop.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        // Reject oversized uploads before they're read into memory.
        services.AddOptions<FormOptions>().Configure<IOptions<RagOptions>>((form, rag) =>
            form.MultipartBodyLengthLimit = rag.Value.Limits.MaxFiles * rag.Value.Limits.MaxFileBytes + 64 * 1024);

        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(UploadsPolicy, context => PerVisitorPerHour(context, l => l.UploadsPerHour));
            limiter.AddPolicy(SearchesPolicy, context => PerVisitorPerHour(context, l => l.SearchesPerHour));
            limiter.AddPolicy(MatchesPolicy, context => PerVisitorPerHour(context, l => l.MatchesPerHour));

            limiter.OnRejected = async (rejected, cancellationToken) =>
            {
                var http = rejected.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }
                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "You've made a lot of requests in the last hour. Please wait a little and try again.",
                    },
                });
            };
        });

        return services;
    }

    /// <summary>A fixed one-hour allowance per IP address.</summary>
    private static RateLimitPartition<string> PerVisitorPerHour(HttpContext context, Func<LimitOptions, int> permits)
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<RagOptions>>().Value.Limits;
        var visitor = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(visitor, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits(limits),
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        });
    }
}
