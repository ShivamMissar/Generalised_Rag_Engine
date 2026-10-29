using Microsoft.Extensions.Options;
using Rag.Core.Abstractions;
using Rag.Core.Indexing;
using Rag.Core.Models;
using Rag.Api.Protection;

namespace Rag.Api.Sessions;

/// <summary>
/// Creates and deletes visitor sessions: each session's documents are chunked, fingerprinted and stored in their
/// own private collection, which is deleted when the visitor asks or when the session expires.
/// </summary>
/// <remarks>
/// <b>In plain English:</b> every visitor gets their own temporary shelf in the library. Nobody else can search it,
/// and it's cleared out after an hour. The sample CV is the exception: it's one shared shelf, built once and reused,
/// so trying the sample doesn't use up the daily AI allowance each time.
/// </remarks>
public sealed class SessionService(
    SessionStore sessions,
    DocumentIndexer indexer,
    IVectorStore vectors,
    EmbeddingDimensions dimensions,
    IDocumentLoader loader,
    DailyUsageBudget budget,
    IOptions<RagOptions> options,
    IHostEnvironment environment)
{
    private const string SampleId = "sample";
    private readonly SemaphoreSlim _sampleLock = new(1, 1);

    /// <summary>Indexes the visitor's documents into a new private session.</summary>
    /// <exception cref="ServiceBusyException">The server is holding as many sessions as it allows.</exception>
    public async Task<UploadSession> CreateAsync(IReadOnlyList<Document> documents, CancellationToken cancellationToken)
    {
        if (sessions.Count >= options.Value.Limits.MaxSessions)
        {
            throw new ServiceBusyException();
        }
        budget.Consume();

        var id = SessionStore.NewId();
        var session = await IndexAsync(id, documents, sessions.NewExpiry(), isSample: false, cancellationToken);
        sessions.Add(session);
        return session;
    }

    /// <summary>Returns the shared sample session, building it on first use.</summary>
    public async Task<UploadSession> GetOrCreateSampleAsync(CancellationToken cancellationToken)
    {
        await _sampleLock.WaitAsync(cancellationToken);
        try
        {
            try
            {
                return sessions.Get(SampleId);
            }
            catch (SessionNotFoundException)
            {
                budget.Consume();
                var samplesPath = Path.GetFullPath(options.Value.SamplesPath, environment.ContentRootPath);
                var documents = await loader.LoadAsync(samplesPath, cancellationToken);
                var session = await IndexAsync(SampleId, documents, DateTimeOffset.MaxValue, isSample: true, cancellationToken);
                sessions.Add(session);
                return session;
            }
        }
        finally
        {
            _sampleLock.Release();
        }
    }

    /// <summary>Deletes a session and its data straight away. The shared sample can't be deleted, so it's left alone.</summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (id == SampleId)
        {
            return;
        }
        if (sessions.Remove(id) is { } session)
        {
            await vectors.DeleteCollectionAsync(session.CollectionName, cancellationToken);
        }
    }

    private async Task<UploadSession> IndexAsync(
        string id,
        IReadOnlyList<Document> documents,
        DateTimeOffset expiresAt,
        bool isSample,
        CancellationToken cancellationToken)
    {
        var embeddingModel = options.Value.IsGemini ? options.Value.Gemini.EmbeddingModel : options.Value.Ollama.EmbeddingModel;
        var collection = new Collection($"session-{id}", embeddingModel, await dimensions.GetAsync(cancellationToken));
        var result = await indexer.IndexAsync(collection, documents, cancellationToken);

        var perDocument = documents
            .Select(d => new SessionDocument(d.Id, result.Chunks.Count(c => c.DocumentId == d.Id)))
            .ToList();
        return new UploadSession(id, collection.Name, perDocument, expiresAt, isSample);
    }
}
