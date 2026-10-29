using System.ClientModel;
using Microsoft.Extensions.AI;

namespace Rag.Api;

/// <summary>
/// A chat client that tries several models in order: if one is overloaded, rate-limited or too slow, it asks the
/// next one instead of failing.
/// </summary>
/// <remarks>
/// <b>In plain English:</b> like phoning a second shop when the first line is engaged. Free AI tiers get busy at peak
/// times ("this model is experiencing high demand"), and a slightly smaller model that answers is better for a
/// visitor than an error. Only temporary problems trigger the fallback. A mistake on our side, such as a wrong API
/// key, is reported straight away.
/// </remarks>
public sealed class FallbackChatClient(IReadOnlyList<IChatClient> clients, ILogger<FallbackChatClient> logger) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        for (var i = 0; ; i++)
        {
            try
            {
                return await clients[i].GetResponseAsync(messageList, options, cancellationToken);
            }
            catch (Exception ex) when (i < clients.Count - 1 && IsTemporary(ex, cancellationToken))
            {
                logger.LogWarning("Chat model {Index} was unavailable ({Reason}); trying the fallback model", i + 1, ex.Message);
            }
        }
    }

    /// <summary>Streaming isn't used by the app, so it goes to the first model without a fallback.</summary>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        clients[0].GetStreamingResponseAsync(messages, options, cancellationToken);

    /// <summary>Describes the first (main) model, e.g. for its name.</summary>
    public object? GetService(Type serviceType, object? serviceKey = null) => clients[0].GetService(serviceType, serviceKey);

    public void Dispose()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// True for problems worth trying another model for: the service being busy (429), failing on its side (5xx),
    /// unreachable, or not answering in time. The caller cancelling isn't one of them.
    /// </summary>
    public static bool IsTemporary(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        ClientResultException e => IsTemporaryStatus(e.Status),
        HttpRequestException => true,
        OperationCanceledException => !cancellationToken.IsCancellationRequested, // a timeout, not the visitor leaving
        _ => false,
    };

    public static bool IsTemporaryStatus(int status) => status is 408 or 429 or >= 500;
}
