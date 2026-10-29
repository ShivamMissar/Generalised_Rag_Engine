namespace Rag.Api;

/// <summary>
/// The engine's settings, read from the "Rag" section of appsettings.json. Paths are relative to the Rag.Api
/// project folder.
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    /// <summary>Where Ollama is running.</summary>
    public Uri OllamaUri { get; set; } = new("http://localhost:11434");

    /// <summary>The chat model that writes match reports.</summary>
    public string ChatModel { get; set; } = "gemma4:12b";

    /// <summary>The embedding model that fingerprints chunks and questions.</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>How many numbers the embedding model produces per fingerprint (768 for nomic-embed-text).</summary>
    public int EmbeddingDimensions { get; set; } = 768;

    /// <summary>The collection that documents are indexed into and searched.</summary>
    public string CollectionName { get; set; } = "candidate";

    /// <summary>The folder of .txt and .md files that <c>POST /api/index</c> loads.</summary>
    public string DocumentsPath { get; set; } = "../../samples/candidate";

    /// <summary>The SQLite database file.</summary>
    public string DatabasePath { get; set; } = "../../data/rag.db";
}
