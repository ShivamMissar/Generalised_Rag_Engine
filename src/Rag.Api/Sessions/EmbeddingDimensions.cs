using Microsoft.Extensions.AI;

namespace Rag.Api.Sessions;

/// <summary>
/// Finds out how many numbers the configured embedding model produces per fingerprint (768 for nomic-embed-text,
/// more for Gemini's models), by asking it once and remembering the answer.
/// </summary>
/// <remarks>
/// Every collection must declare its vector size up front. Asking the model, rather than hard-coding the size,
/// means switching models can't silently break the store. The cost is one small request per server start.
/// </remarks>
public sealed class EmbeddingDimensions(IEmbeddingGenerator<string, Embedding<float>> embedder)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _dimensions;

    public async Task<int> GetAsync(CancellationToken cancellationToken)
    {
        if (_dimensions > 0)
        {
            return _dimensions;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_dimensions == 0)
            {
                _dimensions = (await embedder.GenerateVectorAsync("dimension check", cancellationToken: cancellationToken)).Length;
            }
            return _dimensions;
        }
        finally
        {
            _lock.Release();
        }
    }
}
