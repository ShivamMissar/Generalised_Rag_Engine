namespace Rag.Core.Storage;

/// <summary>
/// Thrown when searching or writing to a collection that hasn't been created yet, which usually means nothing has
/// been indexed. It has its own type so callers (like the API) can catch exactly this case and say "index first".
/// </summary>
public sealed class CollectionNotFoundException(string collectionName)
    : InvalidOperationException($"Collection '{collectionName}' doesn't exist. Index some documents into it first.")
{
    /// <summary>The name of the missing collection.</summary>
    public string CollectionName { get; } = collectionName;
}
