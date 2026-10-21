using Rag.Core.Models;

namespace Rag.Core.Abstractions;

/// <summary>
/// The query side of RAG: takes a natural-language query and returns the most relevant chunks to use
/// as context for the LLM. A retriever usually embeds the query and searches an
/// <see cref="IVectorStore"/>, and may add filtering, re-ranking or hybrid keyword search on top.
/// </summary>
public interface IRetriever
{
    /// <summary>
    /// Retrieves up to <paramref name="topK"/> chunks relevant to <paramref name="query"/> from the named collection.
    /// </summary>
    Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        string collectionName,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
