using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Rag.Api.Sessions;

/// <summary>
/// A visitor's uploaded documents: the private collection they were indexed into, and when they'll be deleted.
/// </summary>
/// <param name="Id">The session ID, which acts as the key to the visitor's data, so it's long and random.</param>
/// <param name="CollectionName">The vector store collection holding the session's chunks.</param>
/// <param name="Documents">The uploaded files and how many chunks each produced.</param>
/// <param name="ExpiresAt">When the session and its data are deleted. <see cref="DateTimeOffset.MaxValue"/> for the shared sample.</param>
/// <param name="IsSample">True for the shared sample session, which visitors can't delete.</param>
public sealed record UploadSession(
    string Id,
    string CollectionName,
    IReadOnlyList<SessionDocument> Documents,
    DateTimeOffset ExpiresAt,
    bool IsSample = false)
{
    public int Chunks => Documents.Sum(d => d.Chunks);
}

/// <summary>One uploaded file in a session.</summary>
public sealed record SessionDocument(string Name, int Chunks);

/// <summary>
/// Keeps track of visitor sessions in memory. Sessions past their expiry are treated as gone straight away, and
/// <see cref="SessionCleanupService"/> deletes their data shortly after.
/// </summary>
public sealed class SessionStore(TimeProvider time, IOptions<RagOptions> options)
{
    private readonly ConcurrentDictionary<string, UploadSession> _sessions = new(StringComparer.Ordinal);

    /// <summary>How many sessions exist, including any that have expired but haven't been cleaned up yet.</summary>
    public int Count => _sessions.Count;

    /// <summary>A new random session ID: 128 random bits, written in URL-safe characters (22 of them).</summary>
    public static string NewId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    /// <summary>When a session created now should expire.</summary>
    public DateTimeOffset NewExpiry() => time.GetUtcNow().AddMinutes(options.Value.Limits.SessionMinutes);

    public void Add(UploadSession session) => _sessions[session.Id] = session;

    /// <summary>Returns the session, or throws if it doesn't exist or has expired.</summary>
    /// <exception cref="SessionNotFoundException">No live session has this ID.</exception>
    public UploadSession Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !_sessions.TryGetValue(id, out var session) || IsExpired(session))
        {
            throw new SessionNotFoundException();
        }
        return session;
    }

    /// <summary>Removes and returns the session, or null if there wasn't one.</summary>
    public UploadSession? Remove(string id) => _sessions.TryRemove(id, out var session) ? session : null;

    /// <summary>Removes every expired session and returns them, so their data can be deleted.</summary>
    public IReadOnlyList<UploadSession> RemoveExpired()
    {
        var removed = new List<UploadSession>();
        foreach (var session in _sessions.Values.Where(IsExpired))
        {
            if (_sessions.TryRemove(session.Id, out _))
            {
                removed.Add(session);
            }
        }
        return removed;
    }

    private bool IsExpired(UploadSession session) => session.ExpiresAt <= time.GetUtcNow();
}

/// <summary>The session ID is missing, wrong, or has expired. Shown to visitors as "upload your CV again".</summary>
public sealed class SessionNotFoundException()
    : Exception("Your uploaded documents have expired or been removed. Upload them again to continue.");
