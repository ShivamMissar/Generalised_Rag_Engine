using Microsoft.Data.Sqlite;
using Rag.Core.Models;
using Rag.Core.Storage;

namespace Rag.Sqlite.Tests;

public sealed class SqliteVectorStoreTests : IDisposable
{
    private static readonly Collection TestCollection = new("test", "fake-model", 2);

    private readonly string _folder = Directory.CreateTempSubdirectory("rag-sqlite-tests-").FullName;
    private readonly string _databasePath;
    private readonly SqliteVectorStore _store;

    public SqliteVectorStoreTests()
    {
        _databasePath = Path.Combine(_folder, "nested", "rag.db");
        _store = new SqliteVectorStore(_databasePath);
    }

    public void Dispose()
    {
        // Connections are pooled, which keeps the file open. Release them so the folder can be deleted.
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private static Chunk Chunk(string id, string documentId, float[] embedding, string content = "text", int index = 0) =>
        new(id, documentId, index, content, new Dictionary<string, string> { ["section"] = "Skills" }) { Embedding = embedding };

    private async Task AddAsync(params Chunk[] chunks)
    {
        await _store.EnsureCollectionAsync(TestCollection);
        await _store.UpsertAsync("test", chunks);
    }

    [Fact]
    public async Task Creates_the_database_file_and_its_folder()
    {
        await _store.EnsureCollectionAsync(TestCollection);

        Assert.True(File.Exists(_databasePath));
    }

    [Fact]
    public async Task Chunks_survive_a_round_trip_through_the_database()
    {
        await AddAsync(Chunk("cv.md#3", "cv.md", [0.25f, -0.5f], "C# and SQL", index: 3));

        // A new store on the same file proves the data was saved to disk, not just kept in memory.
        var reopened = new SqliteVectorStore(_databasePath);
        var result = Assert.Single(await reopened.SearchAsync("test", new float[] { 0.25f, -0.5f }, topK: 5));

        Assert.Equal("cv.md#3", result.Chunk.Id);
        Assert.Equal("cv.md", result.Chunk.DocumentId);
        Assert.Equal(3, result.Chunk.Index);
        Assert.Equal("C# and SQL", result.Chunk.Content);
        Assert.Equal("Skills", result.Chunk.Metadata["section"]);
        Assert.Equal([0.25f, -0.5f], result.Chunk.Embedding.ToArray());
        Assert.Equal(1.0, result.Score, precision: 5);
    }

    [Fact]
    public async Task Search_returns_the_closest_chunks_first_up_to_top_k()
    {
        await AddAsync(
            Chunk("up", "d", [0f, 1f]),
            Chunk("right", "d", [1f, 0f]),
            Chunk("diagonal", "d", [1f, 1f]));

        var results = await _store.SearchAsync("test", new float[] { 1f, 0.1f }, topK: 2);

        Assert.Equal(["right", "diagonal"], results.Select(r => r.Chunk.Id));
    }

    [Fact]
    public async Task Upserting_the_same_id_replaces_the_chunk()
    {
        await AddAsync(Chunk("a", "d", [1f, 0f], "old"));
        await _store.UpsertAsync("test", [Chunk("a", "d", [1f, 0f], "new")]);

        var result = Assert.Single(await _store.SearchAsync("test", new float[] { 1f, 0f }, topK: 5));

        Assert.Equal("new", result.Chunk.Content);
    }

    [Fact]
    public async Task Deleting_a_document_removes_only_its_chunks()
    {
        await AddAsync(Chunk("a#0", "a", [1f, 0f]), Chunk("a#1", "a", [1f, 0f]), Chunk("b#0", "b", [1f, 0f]));

        await _store.DeleteDocumentsAsync("test", ["a"]);

        var result = Assert.Single(await _store.SearchAsync("test", new float[] { 1f, 0f }, topK: 5));
        Assert.Equal("b#0", result.Chunk.Id);
    }

    [Fact]
    public async Task Deleting_a_collection_removes_it_and_its_chunks()
    {
        await AddAsync(Chunk("a", "d", [1f, 0f]));

        await _store.DeleteCollectionAsync("test");

        await Assert.ThrowsAsync<CollectionNotFoundException>(() => _store.SearchAsync("test", new float[] { 1f, 0f }, 5));
        // Recreating it starts empty, so the chunks really were deleted rather than orphaned.
        await _store.EnsureCollectionAsync(TestCollection);
        Assert.Empty(await _store.SearchAsync("test", new float[] { 1f, 0f }, 5));
    }

    [Fact]
    public async Task Collections_are_kept_separate()
    {
        await AddAsync(Chunk("in-test", "d", [1f, 0f]));
        await _store.EnsureCollectionAsync(TestCollection with { Name = "other" });
        await _store.UpsertAsync("other", [Chunk("in-other", "d", [1f, 0f])]);

        var result = Assert.Single(await _store.SearchAsync("other", new float[] { 1f, 0f }, topK: 5));

        Assert.Equal("in-other", result.Chunk.Id);
    }

    [Fact]
    public async Task Ensuring_an_existing_collection_with_a_different_model_throws()
    {
        await _store.EnsureCollectionAsync(TestCollection);
        await _store.EnsureCollectionAsync(TestCollection); // same settings: fine

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.EnsureCollectionAsync(TestCollection with { Dimensions = 3 }));
    }

    [Fact]
    public async Task Using_a_missing_collection_throws()
    {
        await Assert.ThrowsAsync<CollectionNotFoundException>(() => _store.SearchAsync("missing", new float[] { 1f, 0f }, 5));
        await Assert.ThrowsAsync<CollectionNotFoundException>(() => _store.UpsertAsync("missing", []));
    }

    [Fact]
    public async Task Wrong_size_embeddings_are_rejected_and_nothing_is_written()
    {
        await _store.EnsureCollectionAsync(TestCollection);

        // The second chunk is invalid, so the whole batch is rolled back, including the valid first chunk.
        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpsertAsync("test",
            [Chunk("ok", "d", [1f, 0f]), Chunk("bad", "d", [1f, 0f, 0f])]));

        Assert.Empty(await _store.SearchAsync("test", new float[] { 1f, 0f }, topK: 5));
        await Assert.ThrowsAsync<ArgumentException>(() => _store.SearchAsync("test", new float[] { 1f, 0f, 0f }, 5));
    }
}
