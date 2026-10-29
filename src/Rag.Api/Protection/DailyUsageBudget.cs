using Microsoft.Extensions.Options;

namespace Rag.Api.Protection;

/// <summary>
/// A site-wide daily allowance of AI-backed actions (uploads, searches, matches), reset at midnight UTC.
/// </summary>
/// <remarks>
/// Per-visitor rate limits stop one person hammering the site, but many visitors (or a bot using many addresses)
/// could still use up the AI provider's free quota, or run up a bill if billing were turned on. This is the
/// backstop: once the day's allowance is used, AI actions are refused with a friendly message until tomorrow.
/// </remarks>
public sealed class DailyUsageBudget(TimeProvider time, IOptions<RagOptions> options)
{
    private readonly Lock _lock = new();
    private DateOnly _day;
    private int _used;

    /// <summary>How many actions are left today.</summary>
    public int Remaining
    {
        get
        {
            lock (_lock)
            {
                ResetIfNewDay();
                return Math.Max(0, options.Value.Limits.DailyAiRequests - _used);
            }
        }
    }

    /// <summary>Uses one action from today's allowance.</summary>
    /// <exception cref="UsageLimitReachedException">Today's allowance has been used up.</exception>
    public void Consume()
    {
        lock (_lock)
        {
            ResetIfNewDay();
            if (_used >= options.Value.Limits.DailyAiRequests)
            {
                throw new UsageLimitReachedException();
            }
            _used++;
        }
    }

    private void ResetIfNewDay()
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        if (today != _day)
        {
            _day = today;
            _used = 0;
        }
    }
}

/// <summary>The site's daily AI allowance is used up.</summary>
public sealed class UsageLimitReachedException()
    : Exception("The demo has reached its limit for today. Please try again tomorrow, or run the project locally from GitHub.");

/// <summary>The server is at capacity (too many visitors' sessions in memory).</summary>
public sealed class ServiceBusyException()
    : Exception("Lots of people are using the demo right now. Please try again in a few minutes.");
