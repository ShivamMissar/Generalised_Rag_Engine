using Rag.Core.Models;

namespace Rag.Core.Abstractions;

/// <summary>
/// The first stage of the ingestion pipeline. A loader reads raw content from a source (such as a
/// folder of Markdown files or a PDF) and turns it into plain-text <see cref="Document"/>s, ready for chunking.
/// Each format or source type gets its own implementation.
/// </summary>
public interface IDocumentLoader
{
    /// <summary>
    /// Loads every document available from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">A path, URL or other locator that this loader understands.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<IReadOnlyList<Document>> LoadAsync(string source, CancellationToken cancellationToken = default);
}
