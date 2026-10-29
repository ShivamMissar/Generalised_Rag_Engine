using Rag.Core.Abstractions;

namespace Rag.Api.Sessions;

/// <summary>
/// Runs in the background, deleting the data of expired sessions once a minute. Expired sessions already can't be
/// used, so this is about freeing memory and making sure uploaded CVs don't linger.
/// </summary>
public sealed class SessionCleanupService(SessionStore sessions, IVectorStore vectors, TimeProvider time, ILogger<SessionCleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanUpAsync(stoppingToken);
        }
    }

    /// <summary>Deletes every expired session's data. Public so tests can run it without waiting.</summary>
    public async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        var expired = sessions.RemoveExpired();
        foreach (var session in expired)
        {
            await vectors.DeleteCollectionAsync(session.CollectionName, cancellationToken);
        }
        if (expired.Count > 0)
        {
            logger.LogInformation("Deleted {Count} expired session(s)", expired.Count);
        }
    }
}
