using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Rag.Testing;

namespace Rag.Api.Tests;

public class FallbackChatClientTests
{
    private static FallbackChatClient Fallback(params IChatClient[] clients) =>
        new(clients, NullLogger<FallbackChatClient>.Instance);

    [Fact]
    public async Task Uses_the_first_model_when_it_answers()
    {
        var second = new RecordingChatClient("second");
        var client = Fallback(new RecordingChatClient("first"), second);

        var response = await client.GetResponseAsync("hi");

        Assert.Equal("first", response.Text);
        Assert.Empty(second.LastMessages);
    }

    [Fact]
    public async Task Falls_back_when_the_first_model_is_unavailable()
    {
        var client = Fallback(new FailingChatClient(new HttpRequestException("busy")), new RecordingChatClient("second"));

        Assert.Equal("second", (await client.GetResponseAsync("hi")).Text);
    }

    [Fact]
    public async Task Falls_back_when_the_first_model_times_out()
    {
        var client = Fallback(new FailingChatClient(new TaskCanceledException("timed out")), new RecordingChatClient("second"));

        Assert.Equal("second", (await client.GetResponseAsync("hi")).Text);
    }

    [Fact]
    public async Task Does_not_fall_back_for_mistakes_on_our_side()
    {
        var second = new RecordingChatClient("second");
        var client = Fallback(new FailingChatClient(new InvalidOperationException("bad request")), second);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync("hi"));
        Assert.Empty(second.LastMessages);
    }

    [Fact]
    public async Task Does_not_fall_back_when_the_caller_cancels()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var client = Fallback(new FailingChatClient(new OperationCanceledException()), new RecordingChatClient("second"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetResponseAsync("hi", cancellationToken: cancelled.Token));
    }

    [Fact]
    public async Task Reports_the_last_models_error_when_every_model_fails()
    {
        var client = Fallback(new FailingChatClient(new HttpRequestException("first")), new FailingChatClient(new HttpRequestException("second")));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetResponseAsync("hi"));
        Assert.Equal("second", ex.Message);
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(408, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    public void Only_busy_or_failing_service_statuses_are_temporary(int status, bool temporary)
    {
        Assert.Equal(temporary, FallbackChatClient.IsTemporaryStatus(status));
    }

    private sealed class FailingChatClient(Exception error) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromException<ChatResponse>(error);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw error;

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
