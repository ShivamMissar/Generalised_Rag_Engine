namespace Rag.Core.Models;

/// <summary>
/// A named group of chunks that are searched together, such as "cv-and-projects" or "job-ads".
/// Collections keep different corpora apart in the <see cref="Abstractions.IVectorStore"/>. Every chunk
/// in a collection must be embedded with the same model, so their vectors are comparable.
/// </summary>
/// <param name="Name">Unique name of the collection.</param>
/// <param name="EmbeddingModel">The embedding model used for this collection's vectors (for example, "nomic-embed-text").</param>
/// <param name="Dimensions">Length of each embedding vector (768 for nomic-embed-text).</param>
public sealed record Collection(
    string Name,
    string EmbeddingModel,
    int Dimensions);
