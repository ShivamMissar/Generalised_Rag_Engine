using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;
using Rag.Api.Protection;
using Rag.Api.Sessions;
using Rag.Core.Abstractions;
using Rag.Core.Chunking;
using Rag.Core.Indexing;
using Rag.Core.JobMatching;
using Rag.Core.Loading;
using Rag.Core.Retrieval;
using Rag.Core.Storage;

namespace Rag.Api;

/// <summary>
/// Registers the engine's parts with dependency injection (DI).
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b> DI is a parts list. Each part says what it needs (the retriever needs an embedding
/// model and a store), and the framework builds and connects them on request. Because everything is registered by
/// interface, tests can swap in fakes (a fake AI, an in-memory store) without changing any endpoint code.</para>
///
/// <para><b>Swapping the AI provider</b> happens here and only here. The engine only knows the
/// Microsoft.Extensions.AI interfaces (<see cref="IChatClient"/>, <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>),
/// so moving from local Ollama models to Google's hosted Gemini models is a settings change.</para>
/// </remarks>
public static class RagServiceCollectionExtensions
{
    public static IServiceCollection AddRagEngine(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail at startup, with a clear message, rather than on a visitor's first request.
        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            .Validate(o => o.IsOllama || o.IsGemini, "Rag:Provider must be \"Ollama\" or \"Gemini\".")
            .Validate(o => !o.IsGemini || !string.IsNullOrWhiteSpace(o.Gemini.ApiKey),
                "Rag:Provider is Gemini but no API key is set. Set the Rag__Gemini__ApiKey environment variable, " +
                "or locally: dotnet user-secrets set \"Rag:Gemini:ApiKey\" \"<key>\" --project src/Rag.Api")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);

        // AI models: Ollama locally, Gemini for the public demo.
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            return options.IsGemini
                ? GeminiClient(options.Gemini).GetEmbeddingClient(options.Gemini.EmbeddingModel).AsIEmbeddingGenerator()
                : new OllamaApiClient(options.Ollama.Uri, options.Ollama.EmbeddingModel);
        });
        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            return options.IsGemini
                ? GeminiClient(options.Gemini).GetChatClient(options.Gemini.ChatModel).AsIChatClient()
                : new OllamaApiClient(options.Ollama.Uri, options.Ollama.ChatModel);
        });
        // nomic-embed-text was trained with "search_query:" / "search_document:" labels. Gemini's embeddings weren't.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RagOptions>>().Value.IsGemini
            ? EmbeddingPrefixes.None
            : EmbeddingPrefixes.Nomic);

        // Visitors' documents live only in memory, so nothing they upload is ever written to disk.
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();

        // The pipeline steps. They hold no per-request state, so one shared instance of each is fine.
        services.AddSingleton<IDocumentLoader, TextFileLoader>();
        services.AddSingleton<IChunker>(_ => new MarkdownSectionChunker());
        services.AddSingleton<IRetriever, VectorRetriever>();
        services.AddSingleton<DocumentIndexer>();
        services.AddSingleton(sp => new JobMatchAnalyzer(sp.GetRequiredService<IRetriever>(), sp.GetRequiredService<IChatClient>()));

        // Visitor sessions and the safety limits around them.
        services.AddSingleton<EmbeddingDimensions>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<DailyUsageBudget>();
        services.AddHostedService<SessionCleanupService>();

        return services;
    }

    /// <summary>
    /// Gemini accepts the same requests as OpenAI's API at its own address, so the official OpenAI SDK (and its
    /// Microsoft.Extensions.AI adapter) works with it. The SDK also retries automatically when the service is busy.
    /// </summary>
    private static OpenAIClient GeminiClient(GeminiOptions gemini) =>
        new(new ApiKeyCredential(gemini.ApiKey!), new OpenAIClientOptions { Endpoint = gemini.Endpoint });
}
