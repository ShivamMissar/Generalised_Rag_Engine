using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Rag.Core.Abstractions;
using Rag.Core.Chunking;
using Rag.Core.Indexing;
using Rag.Core.JobMatching;
using Rag.Core.Loading;
using Rag.Core.Models;
using Rag.Core.Retrieval;
using Rag.Sqlite;

namespace Rag.Api;

/// <summary>
/// Registers the engine's parts with dependency injection (DI).
/// </summary>
/// <remarks>
/// <b>In plain English:</b> DI is a parts list. Each part says what it needs (the retriever needs an embedding
/// model and a store), and the framework builds and connects them on request. Because everything is registered by
/// interface, tests can swap in fakes (a fake AI, an in-memory store) without changing any endpoint code.
/// </remarks>
public static class RagServiceCollectionExtensions
{
    public static IServiceCollection AddRagEngine(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        // AI models (Ollama). One client per model.
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            return new OllamaApiClient(options.OllamaUri, options.EmbeddingModel);
        });
        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            return new OllamaApiClient(options.OllamaUri, options.ChatModel);
        });

        // Storage. The database path is resolved relative to the project folder.
        services.AddSingleton<IVectorStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            var environment = sp.GetRequiredService<IHostEnvironment>();
            return new SqliteVectorStore(Path.GetFullPath(options.DatabasePath, environment.ContentRootPath));
        });

        // The collection's settings, built from the options.
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            return new Collection(options.CollectionName, options.EmbeddingModel, options.EmbeddingDimensions);
        });

        // The pipeline steps. They hold no per-request state, so one shared instance of each is fine.
        services.AddSingleton(EmbeddingPrefixes.Nomic);
        services.AddSingleton<IDocumentLoader, TextFileLoader>();
        services.AddSingleton<IChunker, MarkdownSectionChunker>(_ => new MarkdownSectionChunker());
        services.AddSingleton<IRetriever, VectorRetriever>();
        services.AddSingleton<DocumentIndexer>();
        services.AddSingleton(sp => new JobMatchAnalyzer(sp.GetRequiredService<IRetriever>(), sp.GetRequiredService<IChatClient>()));

        return services;
    }
}
