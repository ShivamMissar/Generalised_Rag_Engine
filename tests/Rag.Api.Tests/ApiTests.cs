using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Rag.Api;
using Rag.Api.Sessions;
using Rag.Core.Indexing;
using Rag.Testing;

namespace Rag.Api.Tests;

/// <summary>
/// Runs the real API in memory with the AI models swapped for fakes, so the tests are fast and don't need Ollama
/// or an API key. A fake clock lets tests jump forward in time to check that sessions expire.
/// </summary>
public sealed class ApiTests : IDisposable
{
    private const string Cv = "# CV\n## Backend\nC# APIs\n## Data\nSQL schemas\n## Hobbies\ncooking";

    private readonly string _samplesFolder = Directory.CreateTempSubdirectory("rag-api-samples-").FullName;
    private readonly KeywordEmbeddingGenerator _embedder = new("c#", "sql", "cooking");
    private readonly RecordingChatClient _chat = new("Overall match: Strong.");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public ApiTests()
    {
        File.WriteAllText(Path.Combine(_samplesFolder, "sample-cv.md"), "# Sample\n## Skills\nSQL and C#");
    }

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
        Directory.Delete(_samplesFolder, recursive: true);
    }

    /// <summary>Starts the API with fakes, plus any extra settings (e.g. lower limits) for the test.</summary>
    private WebApplicationFactory<Program> Api(params (string Key, string Value)[] settings)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Rag:SamplesPath", _samplesFolder);
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.RemoveAll<IChatClient>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_embedder);
                services.AddSingleton<IChatClient>(_chat);
                services.AddSingleton<TimeProvider>(_clock);
            });
        });
        _factories.Add(factory);
        return factory;
    }

    private static MultipartFormDataContent Files(params (string Name, string Content)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, content) in files)
        {
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "files", name);
        }
        return form;
    }

    private static async Task<SessionResponse> UploadAsync(HttpClient client, params (string Name, string Content)[] files)
    {
        var response = await client.PostAsync("/api/sessions", Files(files));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
    }

    private static async Task<ProblemDetails> ProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;

    // ---- Basics ----

    [Fact]
    public async Task Health_check_reports_healthy()
    {
        Assert.Equal("Healthy", await Api().CreateClient().GetStringAsync("/health"));
    }

    [Fact]
    public async Task Info_describes_the_provider_and_limits()
    {
        var info = await Api().CreateClient().GetFromJsonAsync<InfoResponse>("/api/info");

        Assert.Equal("Local models (Ollama)", info!.Provider);
        Assert.Equal(60, info.SessionMinutes);
        Assert.Equal(3, info.MaxFiles);
        Assert.Equal([".docx", ".md", ".pdf", ".txt"], info.FileTypes);
    }

    // ---- Sessions ----

    [Fact]
    public async Task Uploading_documents_creates_a_private_session()
    {
        var session = await UploadAsync(Api().CreateClient(), ("cv.md", Cv), ("notes.txt", "Some SQL notes"));

        Assert.Equal(22, session.SessionId.Length);
        Assert.Equal([("cv.md", 3), ("notes.txt", 1)], session.Documents.Select(d => (d.Name, d.Chunks)));
        Assert.Equal(4, session.Chunks);
        Assert.Equal(_clock.GetUtcNow().AddMinutes(60), session.ExpiresAt);
        Assert.False(session.IsSample);
    }

    [Fact]
    public async Task Files_with_the_same_name_are_kept_apart()
    {
        var session = await UploadAsync(Api().CreateClient(), ("cv.md", Cv), ("cv.md", "Other SQL work"));

        Assert.Equal(["cv.md", "cv (2).md"], session.Documents.Select(d => d.Name));
    }

    [Fact]
    public async Task Sessions_are_private_to_their_id()
    {
        var client = Api().CreateClient();
        var mine = await UploadAsync(client, ("cv.md", Cv));
        var theirs = await UploadAsync(client, ("other.md", "# Other\nSQL everywhere"));

        var results = await client.GetFromJsonAsync<SearchResponse>($"/api/search?q=SQL&topK=10&sessionId={mine.SessionId}");

        Assert.All(results!.Results, hit => Assert.Equal("cv.md", hit.DocumentId));
        Assert.NotEqual(mine.SessionId, theirs.SessionId);
    }

    [Fact]
    public async Task The_sample_session_is_shared_and_built_only_once()
    {
        var client = Api().CreateClient();
        var before = (await client.GetFromJsonAsync<InfoResponse>("/api/info"))!.RemainingToday;

        var first = await (await client.PostAsync("/api/sessions/sample", null)).Content.ReadFromJsonAsync<SessionResponse>();
        var second = await (await client.PostAsync("/api/sessions/sample", null)).Content.ReadFromJsonAsync<SessionResponse>();

        Assert.Equal("sample", first!.SessionId);
        Assert.Equal("sample", second!.SessionId);
        Assert.True(first.IsSample);
        Assert.Null(first.ExpiresAt);
        Assert.Equal(["sample-cv.md"], first.Documents.Select(d => d.Name));
        Assert.Equal(before - 1, (await client.GetFromJsonAsync<InfoResponse>("/api/info"))!.RemainingToday);
    }

    [Fact]
    public async Task A_session_can_be_checked_and_deleted()
    {
        var client = Api().CreateClient();
        var session = await UploadAsync(client, ("cv.md", Cv));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/sessions/{session.SessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sessions/{session.SessionId}")).StatusCode);

        var after = await client.GetAsync($"/api/search?q=SQL&sessionId={session.SessionId}");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Deleting_the_sample_session_leaves_it_for_everyone_else()
    {
        var client = Api().CreateClient();
        await client.PostAsync("/api/sessions/sample", null);

        await client.DeleteAsync("/api/sessions/sample");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/search?q=SQL&sessionId=sample")).StatusCode);
    }

    [Fact]
    public async Task Sessions_expire_and_their_data_is_deleted()
    {
        var api = Api();
        var client = api.CreateClient();
        var session = await UploadAsync(client, ("cv.md", Cv));

        _clock.Advance(TimeSpan.FromMinutes(61));

        var response = await client.GetAsync($"/api/sessions/{session.SessionId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Upload them again", (await ProblemAsync(response)).Detail);

        var cleanup = api.Services.GetServices<IHostedService>().OfType<SessionCleanupService>().Single();
        await cleanup.CleanUpAsync(CancellationToken.None);
        Assert.Equal(0, api.Services.GetRequiredService<SessionStore>().Count);
    }

    // ---- Upload validation ----

    [Fact]
    public async Task Uploading_nothing_is_rejected()
    {
        var response = await Api().CreateClient().PostAsync("/api/sessions", new MultipartFormDataContent { { new StringContent("x"), "notAFile" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Too_many_files_are_rejected()
    {
        var response = await Api().CreateClient().PostAsync("/api/sessions",
            Files(("a.md", "a"), ("b.md", "b"), ("c.md", "c"), ("d.md", "d")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at most 3 files", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unsupported_files_are_rejected_with_the_reason()
    {
        var response = await Api().CreateClient().PostAsync("/api/sessions", Files(("photo.png", "not text")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("Couldn't read your file", problem.Title);
        Assert.Contains("isn't a supported file type", problem.Detail);
    }

    [Fact]
    public async Task Oversized_files_are_rejected()
    {
        var response = await Api(("Rag:Limits:MaxFileBytes", "10")).CreateClient()
            .PostAsync("/api/sessions", Files(("cv.md", "This is more than ten bytes")));

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Documents_with_too_much_text_are_rejected()
    {
        var response = await Api(("Rag:Limits:MaxDocumentChars", "20")).CreateClient()
            .PostAsync("/api/sessions", Files(("cv.md", Cv)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("characters of text", await response.Content.ReadAsStringAsync());
    }

    // ---- Search and match ----

    [Fact]
    public async Task Search_finds_the_best_matching_chunk_in_the_session()
    {
        var client = Api().CreateClient();
        var session = await UploadAsync(client, ("cv.md", Cv));

        var body = await client.GetFromJsonAsync<SearchResponse>($"/api/search?q=SQL experience&topK=1&sessionId={session.SessionId}");

        var hit = Assert.Single(body!.Results);
        Assert.Equal("CV > Data", hit.Section);
    }

    [Theory]
    [InlineData("/api/search?sessionId=x", "q")]
    [InlineData("/api/search?q=sql&topK=0&sessionId=x", "topK")]
    [InlineData("/api/search?q=sql&topK=21&sessionId=x", "topK")]
    public async Task Search_rejects_bad_input_with_400(string url, string field)
    {
        var response = await Api().CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey(field));
    }

    [Theory]
    [InlineData("")]
    [InlineData("&sessionId=made-up")]
    public async Task Search_without_a_live_session_returns_404(string sessionPart)
    {
        var response = await Api().CreateClient().GetAsync($"/api/search?q=sql{sessionPart}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Session expired", (await ProblemAsync(response)).Title);
    }

    [Fact]
    public async Task Match_returns_requirements_evidence_and_the_report()
    {
        var client = Api().CreateClient();
        var session = await UploadAsync(client, ("cv.md", Cv));

        var response = await client.PostAsJsonAsync("/api/match", new MatchRequest("## Needs\n- C# APIs\n- SQL databases", session.SessionId));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<MatchResponse>();
        Assert.Equal(["C# APIs", "SQL databases"], body!.Requirements);
        Assert.Equal("Overall match: Strong.", body.Report);
        Assert.Contains(body.Evidence, hit => hit.Section == "CV > Backend");
        Assert.Contains(body.Evidence, hit => hit.Section == "CV > Data");
        Assert.Contains("SQL schemas", _chat.LastMessages[1].Text);
    }

    [Fact]
    public async Task Match_rejects_an_empty_or_overlong_job_ad()
    {
        var client = Api(("Rag:Limits:MaxJobAdChars", "50")).CreateClient();
        var session = await UploadAsync(client, ("cv.md", Cv));

        var empty = await client.PostAsJsonAsync("/api/match", new MatchRequest("  ", session.SessionId));
        var tooLong = await client.PostAsJsonAsync("/api/match", new MatchRequest(new string('x', 51), session.SessionId));

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    // ---- Protection ----

    [Fact]
    public async Task The_daily_allowance_stops_ai_actions_until_the_next_day()
    {
        var client = Api(("Rag:Limits:DailyAiRequests", "2")).CreateClient();
        var session = await UploadAsync(client, ("cv.md", Cv));                    // 1
        await client.GetAsync($"/api/search?q=sql&sessionId={session.SessionId}"); // 2

        var refused = await client.GetAsync($"/api/search?q=sql&sessionId={session.SessionId}");
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("Daily limit reached", (await ProblemAsync(refused)).Title);

        _clock.Advance(TimeSpan.FromDays(1));
        var nextDay = await client.PostAsync("/api/sessions", Files(("cv.md", Cv)));
        Assert.Equal(HttpStatusCode.OK, nextDay.StatusCode);
    }

    [Fact]
    public async Task Each_visitor_is_rate_limited()
    {
        var client = Api(("Rag:Limits:UploadsPerHour", "1")).CreateClient();
        await UploadAsync(client, ("cv.md", Cv));

        var second = await client.PostAsync("/api/sessions", Files(("cv.md", Cv)));

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("Too many requests", (await ProblemAsync(second)).Title);
    }

    [Fact]
    public async Task New_sessions_are_refused_when_the_server_is_full()
    {
        var client = Api(("Rag:Limits:MaxSessions", "1")).CreateClient();
        await UploadAsync(client, ("cv.md", Cv));

        var response = await client.PostAsync("/api/sessions", Files(("cv.md", Cv)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task An_unreachable_ai_service_returns_503()
    {
        var api = Api().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new UnreachableEmbeddingGenerator());
        }));
        _factories.Add(api);

        var response = await api.CreateClient().PostAsync("/api/sessions", Files(("cv.md", Cv)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Only_allowed_websites_can_call_the_api_from_a_browser()
    {
        var client = Api(("Rag:AllowedOrigins:0", "https://job-match.example")).CreateClient();

        async Task<string?> AllowedOriginFor(string origin)
        {
            var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/match");
            preflight.Headers.Add("Origin", origin);
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
            var response = await client.SendAsync(preflight);
            return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;
        }

        Assert.Equal("https://job-match.example", await AllowedOriginFor("https://job-match.example"));
        Assert.Null(await AllowedOriginFor("https://somewhere-else.example"));
    }

    // ---- Provider settings ----

    [Fact]
    public void Gemini_without_an_api_key_refuses_to_start()
    {
        var api = Api(("Rag:Provider", "Gemini"));

        var ex = Assert.ThrowsAny<OptionsValidationException>(() => api.CreateClient());

        Assert.Contains("no API key is set", ex.Message);
    }

    [Fact]
    public async Task Gemini_uses_the_openai_compatible_client_and_no_embedding_labels()
    {
        // A real (unfaked) container: only checks wiring, so no requests are sent to Google.
        using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Rag:Provider", "Gemini");
            builder.UseSetting("Rag:Gemini:ApiKey", "test-key");
        });

        var services = api.Services;
        Assert.Contains("OpenAI", services.GetRequiredService<IChatClient>().GetType().Name);
        Assert.Contains("OpenAI", services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>().GetType().Name);
        Assert.Equal(EmbeddingPrefixes.None, services.GetRequiredService<EmbeddingPrefixes>());
        var info = await api.CreateClient().GetFromJsonAsync<InfoResponse>("/api/info");
        Assert.Equal("Google Gemini", info!.Provider);
    }

    [Fact]
    public void An_unknown_provider_refuses_to_start()
    {
        var api = Api(("Rag:Provider", "Mystery"));

        Assert.ThrowsAny<OptionsValidationException>(() => api.CreateClient());
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
