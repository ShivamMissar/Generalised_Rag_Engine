using Rag.Core.Models;

namespace Rag.Core.Abstractions;

/// <summary>
/// Splits a <see cref="Document"/> into <see cref="Chunk"/>s small enough to embed and to fit in a
/// prompt, while keeping enough context in each one to be meaningful. Each chunking strategy (fixed
/// size, sentence-based, heading-aware and so on) is a separate implementation.
/// </summary>
public interface IChunker
{
    /// <summary>
    /// Splits <paramref name="document"/> into ordered chunks. The returned chunks aren't embedded yet.
    /// </summary>
    IReadOnlyList<Chunk> Chunk(Document document);
}
