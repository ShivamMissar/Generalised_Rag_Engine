namespace Rag.Core.Models;

/// <summary>
/// A retrievable slice of a <see cref="Document"/>. An <see cref="Abstractions.IChunker"/> produces
/// chunks, which are then embedded and stored in an <see cref="Abstractions.IVectorStore"/>. A chunk is
/// the unit that gets matched against a query and placed into the LLM prompt as context.
/// </summary>
/// <param name="Id">Unique identifier for this chunk.</param>
/// <param name="DocumentId">The <see cref="Document.Id"/> this chunk was taken from.</param>
/// <param name="Index">Zero-based position of the chunk within its document.</param>
/// <param name="Content">The chunk's text.</param>
/// <param name="Metadata">Attributes inherited from the document, plus any chunk-specific ones (such as section heading).</param>
public sealed record Chunk(
    string Id,
    string DocumentId,
    int Index,
    string Content,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>
    /// The chunk's embedding vector. It's empty until the chunk has been embedded.
    /// </summary>
    public ReadOnlyMemory<float> Embedding { get; init; }
}
