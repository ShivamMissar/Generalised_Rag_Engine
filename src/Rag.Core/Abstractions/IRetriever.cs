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

    /// <summary>
    /// Retrieves results for several queries, returned in the same order as the queries.
    /// </summary>
    /// <remarks>
    /// By default this calls <see cref="RetrieveAsync"/> once per query. Retrievers that call an embedding model
    /// should override it to embed every query in one request. That matters with hosted models, where each request
    /// counts against a rate limit: a job ad with 8 requirements becomes 1 embedding request instead of 8.
    /// </remarks>
    async Task<IReadOnlyList<IReadOnlyList<SearchResult>>> RetrieveManyAsync(
        IReadOnlyList<string> queries,
        string collectionName,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var results = new List<IReadOnlyList<SearchResult>>(queries.Count);
        foreach (var query in queries)
        {
            results.Add(await RetrieveAsync(query, collectionName, topK, cancellationToken));
        }
        return results;
    }
}
