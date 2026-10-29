using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rag.Api;
using Rag.Core.Abstractions;
using Rag.Core.Storage;
using Rag.Testing;

namespace Rag.Api.Tests;

/// <summary>
/// Runs the real API in memory, with the AI models swapped for fakes and the SQLite store for an in-memory one, so
/// the tests are fast and don't need Ollama.
/// </summary>
public sealed class ApiTests : IDisposable
{
    private readonly string _documentsFolder = Directory.CreateTempSubdirectory("rag-api-tests-").FullName;
    private readonly KeywordEmbeddingGenerator _embedder = new("c#", "sql", "cooking");
    private readonly RecordingChatClient _chat = new("Overall match: Strong.");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests()
    {
        File.WriteAllText(Path.Combine(_documentsFolder, "cv.md"), "# CV\n## Backend\nC# APIs\n## Data\nSQL schemas\n## Hobbies\ncooking");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Rag:DocumentsPath", _documentsFolder);
            builder.UseSetting("Rag:EmbeddingModel", "fake-keywords");
            builder.UseSetting("Rag:EmbeddingDimensions", _embedder.Dimensions.ToString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.RemoveAll<IChatClient>();
                services.RemoveAll<IVectorStore>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_embedder);
                services.AddSingleton<IChatClient>(_chat);
                services.AddSingleton<IVectorStore, InMemoryVectorStore>();
            });
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Directory.Delete(_documentsFolder, recursive: true);
    }

    [Fact]
    public async Task Health_check_reports_healthy()
    {
        var response = await _client.GetStringAsync("/health");

        Assert.Equal("Healthy", response);
    }

    [Fact]
    public async Task Index_loads_and_chunks_the_documents_folder()
    {
        var response = await _client.PostAsync("/api/index", null);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IndexResponse>();
        Assert.Equal(new IndexResponse("candidate", Documents: 1, Chunks: 3), body);
    }

    [Fact]
    public async Task Search_returns_the_best_matching_chunks()
    {
        await _client.PostAsync("/api/index", null);

        var body = await _client.GetFromJsonAsync<SearchResponse>("/api/search?q=SQL experience&topK=1");

        var hit = Assert.Single(body!.Results);
        Assert.Equal("cv.md#1", hit.ChunkId);
        Assert.Equal("CV > Data", hit.Section);
        Assert.Contains("SQL schemas", hit.Content);
    }

    [Theory]
    [InlineData("/api/search", "q")]
    [InlineData("/api/search?q=", "q")]
    [InlineData("/api/search?q=sql&topK=0", "topK")]
    [InlineData("/api/search?q=sql&topK=21", "topK")]
    public async Task Search_rejects_bad_input_with_400(string url, string field)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey(field));
    }

    [Fact]
    public async Task Search_before_indexing_returns_409_with_a_hint()
    {
        var response = await _client.GetAsync("/api/search?q=sql");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("POST /api/index", problem!.Detail);
    }

    [Fact]
    public async Task Match_returns_requirements_evidence_and_the_report()
    {
        await _client.PostAsync("/api/index", null);

        var response = await _client.PostAsJsonAsync("/api/match", new MatchRequest("## Needs\n- C# APIs\n- SQL databases"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<MatchResponse>();
        Assert.Equal(["C# APIs", "SQL databases"], body!.Requirements);
        Assert.Equal("Overall match: Strong.", body.Report);
        Assert.Contains(body.Evidence, hit => hit.Section == "CV > Backend");
        Assert.Contains(body.Evidence, hit => hit.Section == "CV > Data");
        // The model really received the evidence.
        Assert.Contains("SQL schemas", _chat.LastMessages[1].Text);
    }

    [Fact]
    public async Task Match_rejects_an_empty_job_ad_with_400()
    {
        var response = await _client.PostAsJsonAsync("/api/match", new MatchRequest("  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ollama_being_unreachable_returns_503()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new UnreachableEmbeddingGenerator());
        }));

        var response = await factory.CreateClient().PostAsync("/api/index", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class UnreachableEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Connection refused");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
