namespace Rag.Api;

/// <summary>
/// The API's settings, read from the "Rag" section of appsettings.json and from environment variables such as
/// <c>Rag__Provider</c>. Relative paths are relative to the Rag.Api project folder.
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    /// <summary>
    /// Which AI models to use: "Ollama" (local models on this machine, the default) or "Gemini" (Google's hosted
    /// models, used for the public demo).
    /// </summary>
    public string Provider { get; set; } = "Ollama";

    public OllamaOptions Ollama { get; set; } = new();

    public GeminiOptions Gemini { get; set; } = new();

    /// <summary>The folder of sample documents behind "Try the sample CV".</summary>
    public string SamplesPath { get; set; } = "../../samples/candidate";

    /// <summary>
    /// Websites allowed to call the API from a browser (CORS), e.g. the Netlify address of the web page. Empty
    /// locally, where the Vite dev server forwards requests, so the browser sees one site.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    public LimitOptions Limits { get; set; } = new();

    public bool IsGemini => Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase);

    public bool IsOllama => Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Local models served by Ollama.</summary>
public sealed class OllamaOptions
{
    public Uri Uri { get; set; } = new("http://localhost:11434");
    public string ChatModel { get; set; } = "gemma4:12b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}

/// <summary>
/// Google's Gemini models, called through Gemini's OpenAI-compatible endpoint. The API key must come from a secret
/// (an environment variable on the server, or <c>dotnet user-secrets</c> locally), never from a committed file.
/// </summary>
public sealed class GeminiOptions
{
    public string? ApiKey { get; set; }
    public Uri Endpoint { get; set; } = new("https://generativelanguage.googleapis.com/v1beta/openai/");
    public string ChatModel { get; set; } = "gemini-2.5-flash";
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
}

/// <summary>
/// Limits that keep a public demo safe: small uploads, short-lived data, and caps on how much AI usage visitors
/// (or bots) can trigger.
/// </summary>
public sealed class LimitOptions
{
    /// <summary>How long uploaded documents are kept before they're deleted automatically.</summary>
    public int SessionMinutes { get; set; } = 60;

    /// <summary>The most visitor sessions held in memory at once.</summary>
    public int MaxSessions { get; set; } = 100;

    public int MaxFiles { get; set; } = 3;

    public long MaxFileBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>The most text (in characters) a session's documents may contain in total. A long CV is about 10,000.</summary>
    public int MaxDocumentChars { get; set; } = 40_000;

    public int MaxJobAdChars { get; set; } = 10_000;

    /// <summary>
    /// The most AI-backed actions (uploads, searches, matches) the whole site will perform per day (UTC). A backstop
    /// that protects the provider's free-tier quota, and the bill if billing is ever turned on.
    /// </summary>
    public int DailyAiRequests { get; set; } = 300;

    // Per-visitor limits, counted by IP address over a rolling hour.
    public int UploadsPerHour { get; set; } = 10;
    public int SearchesPerHour { get; set; } = 100;
    public int MatchesPerHour { get; set; } = 20;
}
