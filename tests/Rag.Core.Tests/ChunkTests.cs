using Rag.Core.Models;

namespace Rag.Core.Tests;

public class ChunkTests
{
    [Fact]
    public void New_chunk_has_no_embedding()
    {
        var chunk = new Chunk("c1", "d1", 0, "Some text", new Dictionary<string, string>());

        Assert.True(chunk.Embedding.IsEmpty);
    }
}
