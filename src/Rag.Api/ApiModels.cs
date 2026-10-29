using Rag.Core.Models;

namespace Rag.Api;

// The shapes of the JSON the API sends and receives. They're kept separate from the engine's own models, so the
// engine can change internally without breaking the front end that calls the API.

/// <summary>Result of <c>POST /api/index</c>.</summary>
public sealed record IndexResponse(string Collection, int Documents, int Chunks);

/// <summary>Result of <c>GET /api/search</c>.</summary>
public sealed record SearchResponse(string Query, IReadOnlyList<ChunkHit> Results);

/// <summary>Body of <c>POST /api/match</c>.</summary>
public sealed record MatchRequest(string? JobAd);

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
