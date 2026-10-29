using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rag.Core.Abstractions;
using Rag.Core.Models;
using Rag.Core.Similarity;
using Rag.Core.Storage;

namespace Rag.Sqlite;

/// <summary>
/// An <see cref="IVectorStore"/> that saves collections and chunks in a single SQLite database file, so
/// documents only need to be fingerprinted once rather than on every run.
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b> this is the filing cabinet. Each chunk is saved as a row holding its text,
/// its metadata (as JSON) and its fingerprint (768 numbers packed into bytes).</para>
///
/// <para><b>How search works:</b> SQLite itself doesn't know how to compare fingerprints. So a search reads
/// every chunk in the collection back into memory and ranks them with <see cref="Ranking.TopK"/>, which uses
/// cosine similarity. That's fine at CV scale. pgvector (planned later) does the comparison inside the
/// database, with an index, for much larger collections.</para>
/// </remarks>
public sealed class SqliteVectorStore : IVectorStore
{
    // Two tables: one row per collection, and one row per chunk. Deleting a collection deletes its chunks.
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS collections (
            name            TEXT    PRIMARY KEY,
            embedding_model TEXT    NOT NULL,
            dimensions      INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS chunks (
            collection  TEXT    NOT NULL REFERENCES collections(name) ON DELETE CASCADE,
            id          TEXT    NOT NULL,
            document_id TEXT    NOT NULL,
            idx         INTEGER NOT NULL,
            content     TEXT    NOT NULL,
            metadata    TEXT    NOT NULL,  -- JSON object of string keys and values
            embedding   BLOB    NOT NULL,  -- float32 values, 4 bytes each
            PRIMARY KEY (collection, id)
        );

        CREATE INDEX IF NOT EXISTS ix_chunks_document ON chunks (collection, document_id);
        """;

    private readonly string _connectionString;
    private bool _schemaCreated;

    /// <param name="databasePath">Path of the database file. It (and its folder) is created if it doesn't exist.</param>
    public SqliteVectorStore(string databasePath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true, // SQLite only enforces REFERENCES (and ON DELETE CASCADE) when this is on.
        }.ToString();
    }

    /// <inheritdoc />
    public async Task EnsureCollectionAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);

        var existing = await FindCollectionAsync(connection, collection.Name, cancellationToken);
        if (existing is not null)
        {
            VectorStoreChecks.EnsureSameSettings(existing, collection);
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO collections (name, embedding_model, dimensions) VALUES ($name, $model, $dimensions)";
        command.Parameters.AddWithValue("$name", collection.Name);
        command.Parameters.AddWithValue("$model", collection.EmbeddingModel);
        command.Parameters.AddWithValue("$dimensions", collection.Dimensions);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(string collectionName, IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var collection = await GetCollectionAsync(connection, collectionName, cancellationToken);

        // One transaction for the whole batch: it's much faster than one per row, and if anything fails,
        // nothing is half-written.
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO chunks (collection, id, document_id, idx, content, metadata, embedding)
            VALUES ($collection, $id, $documentId, $idx, $content, $metadata, $embedding)
            """;
        var collectionParam = command.Parameters.Add("$collection", SqliteType.Text);
        var idParam = command.Parameters.Add("$id", SqliteType.Text);
        var documentIdParam = command.Parameters.Add("$documentId", SqliteType.Text);
        var indexParam = command.Parameters.Add("$idx", SqliteType.Integer);
        var contentParam = command.Parameters.Add("$content", SqliteType.Text);
        var metadataParam = command.Parameters.Add("$metadata", SqliteType.Text);
        var embeddingParam = command.Parameters.Add("$embedding", SqliteType.Blob);

        foreach (var chunk in chunks)
        {
            VectorStoreChecks.EnsureEmbeddingFits(collection, chunk);

            collectionParam.Value = collectionName;
            idParam.Value = chunk.Id;
            documentIdParam.Value = chunk.DocumentId;
            indexParam.Value = chunk.Index;
            contentParam.Value = chunk.Content;
            metadataParam.Value = JsonSerializer.Serialize(chunk.Metadata);
            // Reinterpret the floats as raw bytes (4 per float) to store them compactly.
            embeddingParam.Value = MemoryMarshal.AsBytes(chunk.Embedding.Span).ToArray();
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteDocumentsAsync(string collectionName, IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await GetCollectionAsync(connection, collectionName, cancellationToken);

        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM chunks WHERE collection = $collection AND document_id = $documentId";
        command.Parameters.AddWithValue("$collection", collectionName);
        var documentIdParam = command.Parameters.Add("$documentId", SqliteType.Text);

        foreach (var documentId in documentIds)
        {
            documentIdParam.Value = documentId;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var collection = await GetCollectionAsync(connection, collectionName, cancellationToken);
        VectorStoreChecks.EnsureQueryFits(collection, queryEmbedding);

        // Load every chunk in the collection, then rank them in memory (see the class remarks).
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, document_id, idx, content, metadata, embedding
            FROM chunks
            WHERE collection = $collection
            """;
        command.Parameters.AddWithValue("$collection", collectionName);

        var chunks = new List<Chunk>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4)) ?? [];
            // Turn the stored bytes back into floats: the reverse of what UpsertAsync did.
            var embedding = MemoryMarshal.Cast<byte, float>((byte[])reader.GetValue(5)).ToArray();

            chunks.Add(new Chunk(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), metadata)
            {
                Embedding = embedding,
            });
        }

        return Ranking.TopK(chunks, queryEmbedding, topK);
    }

    /// <summary>Opens a connection, creating the tables the first time.</summary>
    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!_schemaCreated)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = Schema;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _schemaCreated = true;
        }

        return connection;
    }

    private static async Task<Collection> GetCollectionAsync(SqliteConnection connection, string name, CancellationToken cancellationToken) =>
        await FindCollectionAsync(connection, name, cancellationToken) ?? throw VectorStoreChecks.CollectionNotFound(name);

    private static async Task<Collection?> FindCollectionAsync(SqliteConnection connection, string name, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT embedding_model, dimensions FROM collections WHERE name = $name";
        command.Parameters.AddWithValue("$name", name);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new Collection(name, reader.GetString(0), reader.GetInt32(1))
            : null;
    }
}
