namespace Rag.Core.Models;

/// <summary>
/// A whole source document (such as a CV, a project write-up or a job ad) as loaded from disk or
/// another source, before chunking. It's the unit an <see cref="Abstractions.IDocumentLoader"/>
/// produces and an <see cref="Abstractions.IChunker"/> consumes.
/// </summary>
/// <param name="Id">Stable identifier, used to trace chunks and search results back to their source.</param>
/// <param name="Source">Where the document came from, such as a file path or URL.</param>
/// <param name="Content">The document's full plain-text content.</param>
/// <param name="Metadata">Free-form attributes (title, type, date and so on) that are carried through to chunks.</param>
public sealed record Document(
    string Id,
    string Source,
    string Content,
    IReadOnlyDictionary<string, string> Metadata);
