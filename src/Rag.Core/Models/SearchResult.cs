namespace Rag.Core.Models;

/// <summary>
/// A single hit returned by an <see cref="Abstractions.IVectorStore"/> search or an
/// <see cref="Abstractions.IRetriever"/>: a matching chunk and how relevant it is to the query.
/// </summary>
/// <param name="Chunk">The matching chunk.</param>
/// <param name="Score">Relevance score. Higher means more relevant, and the scale depends on the similarity measure used.</param>
public sealed record SearchResult(
    Chunk Chunk,
    double Score);
