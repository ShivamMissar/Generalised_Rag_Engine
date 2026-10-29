using Rag.Core.Chunking;
using Rag.Core.Indexing;
using Rag.Core.Models;
using Rag.Core.Retrieval;
using Rag.Core.Storage;
using Rag.Core.Tests.Fakes;

namespace Rag.Core.Tests;

public class IndexingAndRetrievalTests
{
    private readonly KeywordEmbeddingGenerator _embedder = new("c#", "sql", "cooking");
    private readonly InMemoryVectorStore _store = new();
    private readonly Collection _collection;

    public IndexingAndRetrievalTests()
    {
        _collection = new Collection("test", "keywords", _embedder.Dimensions);
    }

    private DocumentIndexer Indexer(EmbeddingPrefixes prefixes) =>
        new(new MarkdownSectionChunker(), _embedder, _store, prefixes);

    [Fact]
    public async Task Indexing_stores_every_chunk_with_an_embedding()
    {
        var doc = TestData.Document("cv.md", "# CV\n## Backend\nC# and C# again\n## Data\nSQL work");

        var result = await Indexer(EmbeddingPrefixes.None).IndexAsync(_collection, [doc]);

        Assert.Equal(1, result.DocumentCount);
        Assert.Equal(["cv.md#0", "cv.md#1"], result.Chunks.Select(c => c.Id));
        Assert.All(result.Chunks, c => Assert.Equal(_embedder.Dimensions, c.Embedding.Length));
    }

    [Fact]
    public async Task Indexing_embeds_chunks_with_the_document_prefix()
    {
        var doc = TestData.Document("d", "Some text");

        await Indexer(new EmbeddingPrefixes("doc: ", "query: ")).IndexAsync(_collection, [doc]);

        Assert.Equal(["doc: Some text"], _embedder.Inputs);
    }

    [Fact]
    public async Task Re_indexing_a_shorter_document_removes_its_old_chunks()
    {
        var indexer = Indexer(EmbeddingPrefixes.None);
        await indexer.IndexAsync(_collection, [TestData.Document("d", "# A\none\n## B\ntwo\n## C\nthree")]);

        await indexer.IndexAsync(_collection, [TestData.Document("d", "# A\nonly one now")]);

        var results = await _store.SearchAsync("test", _embedder.Vector("anything"), topK: 10);
        Assert.Equal(["d#0"], results.Select(r => r.Chunk.Id));
    }

    [Fact]
    public async Task Retriever_finds_the_chunk_sharing_the_question_keywords()
    {
        var doc = TestData.Document("cv.md", "# CV\n## Backend\nC# APIs\n## Data\nSQL schemas\n## Hobbies\ncooking");
        await Indexer(EmbeddingPrefixes.None).IndexAsync(_collection, [doc]);
        var retriever = new VectorRetriever(_embedder, _store, EmbeddingPrefixes.None);

        var results = await retriever.RetrieveAsync("SQL experience", "test", topK: 1);

        Assert.Equal("CV > Data", Assert.Single(results).Chunk.Metadata["section"]);
    }

    [Fact]
    public async Task Retriever_embeds_the_question_with_the_query_prefix()
    {
        await _store.EnsureCollectionAsync(_collection);
        var retriever = new VectorRetriever(_embedder, _store, new EmbeddingPrefixes("doc: ", "query: "));

        await retriever.RetrieveAsync("SQL experience", "test", topK: 3);

        Assert.Equal(["query: SQL experience"], _embedder.Inputs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Retriever_rejects_an_empty_question(string question)
    {
        var retriever = new VectorRetriever(_embedder, _store, EmbeddingPrefixes.None);

        await Assert.ThrowsAsync<ArgumentException>(() => retriever.RetrieveAsync(question, "test"));
    }
}
