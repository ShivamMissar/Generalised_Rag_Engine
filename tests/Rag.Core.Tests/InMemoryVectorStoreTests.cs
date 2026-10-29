using Rag.Core.Models;
using Rag.Core.Storage;

namespace Rag.Core.Tests;

public class InMemoryVectorStoreTests
{
    private static readonly Collection TestCollection = new("test", "fake-model", 2);
    private readonly InMemoryVectorStore _store = new();

    private async Task<InMemoryVectorStore> StoreWith(params Chunk[] chunks)
    {
        await _store.EnsureCollectionAsync(TestCollection);
        await _store.UpsertAsync("test", chunks);
        return _store;
    }

    [Fact]
    public async Task Search_returns_the_closest_chunks_first()
    {
        await StoreWith(
            TestData.Chunk("right", "d", [1f, 0f]),
            TestData.Chunk("diagonal", "d", [1f, 1f]),
            TestData.Chunk("up", "d", [0f, 1f]));

        var results = await _store.SearchAsync("test", new float[] { 1f, 0.1f }, topK: 3);

        Assert.Equal(["right", "diagonal", "up"], results.Select(r => r.Chunk.Id));
        Assert.True(results[0].Score > results[1].Score && results[1].Score > results[2].Score);
    }

    [Fact]
    public async Task Search_returns_at_most_top_k()
    {
        await StoreWith(
            TestData.Chunk("a", "d", [1f, 0f]),
            TestData.Chunk("b", "d", [0f, 1f]),
            TestData.Chunk("c", "d", [1f, 1f]));

        Assert.Single(await _store.SearchAsync("test", new float[] { 1f, 0f }, topK: 1));
    }

    [Fact]
    public async Task Ties_are_ordered_by_chunk_id()
    {
        await StoreWith(TestData.Chunk("b", "d", [1f, 0f]), TestData.Chunk("a", "d", [1f, 0f]));

        var results = await _store.SearchAsync("test", new float[] { 1f, 0f }, topK: 2);

        Assert.Equal(["a", "b"], results.Select(r => r.Chunk.Id));
    }

    [Fact]
    public async Task Upserting_the_same_id_replaces_the_chunk()
    {
        await StoreWith(TestData.Chunk("a", "d", [1f, 0f], "old"));
        await _store.UpsertAsync("test", [TestData.Chunk("a", "d", [1f, 0f], "new")]);

        var result = Assert.Single(await _store.SearchAsync("test", new float[] { 1f, 0f }, topK: 5));

        Assert.Equal("new", result.Chunk.Content);
    }

    [Fact]
    public async Task Deleting_a_document_removes_only_its_chunks()
    {
        await StoreWith(
            TestData.Chunk("a#0", "a", [1f, 0f]),
            TestData.Chunk("a#1", "a", [1f, 0f]),
            TestData.Chunk("b#0", "b", [1f, 0f]));

        await _store.DeleteDocumentsAsync("test", ["a"]);

        var result = Assert.Single(await _store.SearchAsync("test", new float[] { 1f, 0f }, topK: 5));
        Assert.Equal("b#0", result.Chunk.Id);
    }

    [Fact]
    public async Task Deleting_a_collection_removes_it_and_its_chunks()
    {
        await StoreWith(TestData.Chunk("a", "d", [1f, 0f]));

        await _store.DeleteCollectionAsync("test");

        await Assert.ThrowsAsync<CollectionNotFoundException>(() => _store.SearchAsync("test", new float[] { 1f, 0f }, 5));
        await _store.DeleteCollectionAsync("test"); // deleting again is fine
    }

    [Fact]
    public async Task Collections_are_kept_separate()
    {
        await StoreWith(TestData.Chunk("in-test", "d", [1f, 0f]));
        await _store.EnsureCollectionAsync(TestCollection with { Name = "other" });
        await _store.UpsertAsync("other", [TestData.Chunk("in-other", "d", [1f, 0f])]);

        var result = Assert.Single(await _store.SearchAsync("other", new float[] { 1f, 0f }, topK: 5));

        Assert.Equal("in-other", result.Chunk.Id);
    }

    [Fact]
    public async Task Ensuring_an_existing_collection_with_the_same_settings_is_fine()
    {
        await _store.EnsureCollectionAsync(TestCollection);
        await _store.EnsureCollectionAsync(TestCollection);
    }

    [Fact]
    public async Task Ensuring_an_existing_collection_with_a_different_model_throws()
    {
        await _store.EnsureCollectionAsync(TestCollection);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.EnsureCollectionAsync(TestCollection with { EmbeddingModel = "other-model" }));
        Assert.Contains("other-model", ex.Message);
    }

    [Fact]
    public async Task Using_a_missing_collection_throws()
    {
        await Assert.ThrowsAsync<CollectionNotFoundException>(() => _store.SearchAsync("missing", new float[] { 1f, 0f }, 5));
        await Assert.ThrowsAsync<CollectionNotFoundException>(() => _store.UpsertAsync("missing", []));
    }

    [Fact]
    public async Task Chunks_without_embeddings_or_with_the_wrong_size_are_rejected()
    {
        await _store.EnsureCollectionAsync(TestCollection);

        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpsertAsync("test", [TestData.Chunk("a", "d", [])]));
        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpsertAsync("test", [TestData.Chunk("a", "d", [1f, 2f, 3f])]));
    }

    [Fact]
    public async Task A_query_of_the_wrong_size_is_rejected()
    {
        await StoreWith(TestData.Chunk("a", "d", [1f, 0f]));

        await Assert.ThrowsAsync<ArgumentException>(() => _store.SearchAsync("test", new float[] { 1f, 0f, 0f }, 5));
    }
}
