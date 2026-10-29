using Rag.Api.Sessions;
using Rag.Core.Models;

namespace Rag.Api;

// The shapes of the JSON the API sends and receives. They're kept separate from the engine's own models, so the
// engine can change internally without breaking the front end that calls the API.

/// <summary>Result of <c>GET /api/info</c>: what the server is running and its limits, so the page can explain them.</summary>
public sealed record InfoResponse(
    string Provider,
    string ChatModel,
    int SessionMinutes,
    int MaxFiles,
    double MaxFileMegabytes,
    IReadOnlyList<string> FileTypes,
    int RemainingToday);

/// <summary>A visitor session: their uploaded documents and when they'll be deleted (null for the shared sample).</summary>
public sealed record SessionResponse(
    string SessionId,
    IReadOnlyList<SessionDocumentResponse> Documents,
    int Chunks,
    DateTimeOffset? ExpiresAt,
    bool IsSample)
{
    public static SessionResponse From(UploadSession session) => new(
        session.Id,
        session.Documents.Select(d => new SessionDocumentResponse(d.Name, d.Chunks)).ToList(),
        session.Chunks,
        session.IsSample ? null : session.ExpiresAt,
        session.IsSample);
}

public sealed record SessionDocumentResponse(string Name, int Chunks);

/// <summary>Result of <c>GET /api/search</c>.</summary>
public sealed record SearchResponse(string Query, IReadOnlyList<ChunkHit> Results);

/// <summary>Body of <c>POST /api/match</c>.</summary>
public sealed record MatchRequest(string? JobAd, string? SessionId);

/// <summary>
/// Result of <c>POST /api/match</c>. The evidence list is numbered the same way as the citations in the report,
/// so <c>Evidence[0]</c> is "[1]".
/// </summary>
public sealed record MatchResponse(IReadOnlyList<string> Requirements, IReadOnlyList<ChunkHit> Evidence, string Report);

/// <summary>One retrieved chunk, with its score.</summary>
public sealed record ChunkHit(string ChunkId, string DocumentId, string Section, double Score, string Content)
{
    public static ChunkHit From(SearchResult result) => new(
        result.Chunk.Id,
        result.Chunk.DocumentId,
        result.Chunk.Metadata.GetValueOrDefault("section", ""),
        Math.Round(result.Score, 4),
        result.Chunk.Content);
}
