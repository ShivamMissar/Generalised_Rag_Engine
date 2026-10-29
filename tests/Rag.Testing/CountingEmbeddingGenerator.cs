using Microsoft.Extensions.AI;

namespace Rag.Testing;

/// <summary>Wraps another embedding generator and counts how many requests are made to it.</summary>
public sealed class CountingEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> inner) : IEmbeddingGenerator<string, Embedding<float>>
{
    public int Requests { get; private set; }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Requests++;
        return inner.GenerateAsync(values, options, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
