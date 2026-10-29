using Microsoft.Extensions.AI;

namespace Rag.Core.Tests.Fakes;

/// <summary>A stand-in chat model that always gives the same reply, and records what it was sent.</summary>
internal sealed class RecordingChatClient(string reply) : IChatClient
{
    public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];
    public ChatOptions? LastOptions { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        LastMessages = messages.ToList();
        LastOptions = options;
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
