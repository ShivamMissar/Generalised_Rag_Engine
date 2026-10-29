using Rag.Core.Models;

namespace Rag.Core.Tests;

internal static class TestData
{
    public static Document Document(string id, string content) =>
        new(id, $"/docs/{id}", content, new Dictionary<string, string> { ["fileName"] = id });

    public static Chunk Chunk(string id, string documentId, float[] embedding, string content = "text") =>
        new(id, documentId, 0, content, new Dictionary<string, string>()) { Embedding = embedding };

    public static SearchResult Result(string chunkId, double score, string content = "text") =>
        new(new Chunk(chunkId, "doc", 0, content, new Dictionary<string, string>()), score);
}
