using Microsoft.Extensions.AI;

namespace Rag.Core.Tests.Fakes;

/// <summary>
/// A stand-in for a real embedding model, so tests run instantly without Ollama. Each dimension counts how often
/// one keyword appears in the text, so texts sharing keywords get similar vectors. The last dimension is always 1,
/// so no vector is ever all zeros.
/// </summary>
internal sealed class KeywordEmbeddingGenerator(params string[] keywords) : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>Every text this generator was asked to embed, in order.</summary>
    public List<string> Inputs { get; } = [];

    public int Dimensions => keywords.Length + 1;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var texts = values.ToList();
        Inputs.AddRange(texts);
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(texts.Select(Embed)));
    }

    public float[] Vector(string text) => Embed(text).Vector.ToArray();

    private Embedding<float> Embed(string text)
    {
        var vector = new float[Dimensions];
        for (var i = 0; i < keywords.Length; i++)
        {
            vector[i] = CountOccurrences(text, keywords[i]);
        }
        vector[^1] = 1;
        return new Embedding<float>(vector);
    }

    private static int CountOccurrences(string text, string keyword)
    {
        var count = 0;
        for (var at = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase); at >= 0;
             at = text.IndexOf(keyword, at + keyword.Length, StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }
        return count;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
