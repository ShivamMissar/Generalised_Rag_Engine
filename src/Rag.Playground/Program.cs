// The playground: a small command-line tool that runs the whole RAG engine against your local Ollama models.
//
// Run from the src folder:
//   dotnet run --project Rag.Playground -- index                      Load, chunk, fingerprint and save the sample CV
//   dotnet run --project Rag.Playground -- search "<question>"        Show the best-matching chunks for a question
//   dotnet run --project Rag.Playground -- match [job-ad-file]        Job-match report (default: the sample job ad)
//   dotnet run --project Rag.Playground -- eval                       Score search quality on evals/retrieval.json
//
// (Everything after "--" is passed to this program rather than to dotnet.)

using Microsoft.Extensions.AI;
using OllamaSharp;
using Rag.Core.Chunking;
using Rag.Core.Evaluation;
using Rag.Core.Indexing;
using Rag.Core.JobMatching;
using Rag.Core.Loading;
using Rag.Core.Models;
using Rag.Core.Retrieval;
using Rag.Core.Storage;
using Rag.Sqlite;

// ---- Settings --------------------------------------------------------------------------------------------------

var ollamaUri = new Uri("http://localhost:11434");
const string chatModel = "gemma4:12b";
const string embeddingModel = "nomic-embed-text";
const int embeddingDimensions = 768; // nomic-embed-text produces 768 numbers per fingerprint
const string collectionName = "candidate";

var repoRoot = FindRepoRoot();
var candidateDocsPath = Path.Combine(repoRoot, "samples", "candidate");
var defaultJobAdPath = Path.Combine(repoRoot, "samples", "job-ads", "backend-developer.md");
var databasePath = Path.Combine(repoRoot, "data", "rag.db"); // git-ignored
var evalSetPath = Path.Combine(repoRoot, "evals", "retrieval.json");

// ---- The engine's parts -----------------------------------------------------------------------------------------

// OllamaApiClient implements the Microsoft.Extensions.AI interfaces, and everything else only sees the interfaces,
// so the model provider could be swapped without touching the engine.
IEmbeddingGenerator<string, Embedding<float>> embedder = new OllamaApiClient(ollamaUri, embeddingModel);
IChatClient chat = new OllamaApiClient(ollamaUri, chatModel);

var collection = new Collection(collectionName, embeddingModel, embeddingDimensions);
var prefixes = EmbeddingPrefixes.Nomic;
var loader = new TextFileLoader();
var chunker = new MarkdownSectionChunker();
var store = new SqliteVectorStore(databasePath);
var retriever = new VectorRetriever(embedder, store, prefixes);

// ---- Commands ---------------------------------------------------------------------------------------------------

try
{
    switch (args.FirstOrDefault()?.ToLowerInvariant())
    {
        case "index":
            await IndexAsync();
            break;
        case "search" when args.Length >= 2:
            await SearchAsync(string.Join(' ', args.Skip(1)));
            break;
        case "match":
            await MatchAsync(args.Length >= 2 ? args[1] : defaultJobAdPath);
            break;
        case "eval":
            await EvalAsync();
            break;
        default:
            PrintUsage();
            break;
    }
}
catch (CollectionNotFoundException ex)
{
    Console.WriteLine($"{ex.Message}\nRun the 'index' command first.");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Couldn't reach Ollama at {ollamaUri} ({ex.Message}). Is it running? Try: brew services start ollama");
}

// Load → chunk → fingerprint → save, then list the chunks that were stored.
async Task IndexAsync()
{
    var documents = await loader.LoadAsync(candidateDocsPath);
    var indexer = new DocumentIndexer(chunker, embedder, store, prefixes);
    var result = await indexer.IndexAsync(collection, documents);

    Console.WriteLine($"Indexed {result.DocumentCount} documents into {result.Chunks.Count} chunks in {databasePath}\n");
    foreach (var chunk in result.Chunks)
    {
        Console.WriteLine($"{chunk.Id,-30} {chunk.Content.Length,4} chars   {chunk.Metadata["section"]}");
    }
}

// Fingerprint the question and show the closest chunks with their scores.
async Task SearchAsync(string question)
{
    var results = await retriever.RetrieveAsync(question, collectionName, topK: 5);

    Console.WriteLine($"Top {results.Count} chunks for \"{question}\":\n");
    foreach (var result in results)
    {
        Console.WriteLine($"{result.Score:F3}  {result.Chunk.Id}");
        Console.WriteLine($"       {Preview(result.Chunk.Content)}\n");
    }
}

// The full job-application assistant: requirements → evidence → AI report.
async Task MatchAsync(string jobAdPath)
{
    var jobAd = await File.ReadAllTextAsync(jobAdPath);
    var analyzer = new JobMatchAnalyzer(retriever, chat);

    Console.WriteLine($"Analysing {Path.GetFileName(jobAdPath)} with {chatModel}. This can take up to a minute...\n");
    var result = await analyzer.AnalyzeAsync(jobAd, collectionName);

    Console.WriteLine("Requirements found in the ad:");
    foreach (var requirement in result.Requirements)
    {
        Console.WriteLine($"  - {requirement}");
    }

    Console.WriteLine("\nEvidence given to the AI:");
    for (var i = 0; i < result.Evidence.Count; i++)
    {
        Console.WriteLine($"  [{i + 1}] {result.Evidence[i].Score:F3}  {result.Evidence[i].Chunk.Id}");
    }

    Console.WriteLine($"\n---- Report ----\n\n{result.Report}");
}

// Run the eval set twice, with and without prefixes, each in a fresh in-memory index so it doesn't touch data/rag.db.
async Task EvalAsync()
{
    const int topK = 3;
    var evalSet = await EvalSet.LoadAsync(evalSetPath);
    var documents = await loader.LoadAsync(candidateDocsPath);

    var settings = new (string Name, EmbeddingPrefixes Prefixes)[]
    {
        ("with prefixes", EmbeddingPrefixes.Nomic),
        ("no prefixes", EmbeddingPrefixes.None),
    };

    var reports = new List<(string Name, EvalReport Report)>();
    foreach (var (name, settingPrefixes) in settings)
    {
        var memoryStore = new InMemoryVectorStore();
        await new DocumentIndexer(chunker, embedder, memoryStore, settingPrefixes).IndexAsync(collection, documents);
        var evaluator = new RetrievalEvaluator(new VectorRetriever(embedder, memoryStore, settingPrefixes));
        reports.Add((name, await evaluator.RunAsync(evalSet, collectionName, topK)));
    }

    Console.WriteLine($"{evalSet.Cases.Count} questions, top {topK} results each.\n");
    Console.WriteLine($"{"",-40} {string.Join("  ", reports.Select(r => $"{r.Name,-24}"))}");
    Console.WriteLine($"{"Question",-40} {string.Join("  ", reports.Select(_ => $"{"Recall",-8}{"RR",-16}"))}");
    for (var i = 0; i < evalSet.Cases.Count; i++)
    {
        var question = Truncate(evalSet.Cases[i].Question, 38);
        var scores = reports.Select(r => $"{r.Report.Cases[i].Recall,-8:F2}{r.Report.Cases[i].ReciprocalRank,-16:F2}");
        Console.WriteLine($"{question,-40} {string.Join("  ", scores)}");
    }

    Console.WriteLine();
    foreach (var (name, report) in reports)
    {
        Console.WriteLine($"{name,-15} Recall@{topK} {report.MeanRecall:F2}   MRR {report.MeanReciprocalRank:F2}   Hit rate@{topK} {report.HitRate:F2}");
    }
}

void PrintUsage()
{
    Console.WriteLine("""
        Usage (from the src folder):
          dotnet run --project Rag.Playground -- index
          dotnet run --project Rag.Playground -- search "<question>"
          dotnet run --project Rag.Playground -- match [job-ad-file]
          dotnet run --project Rag.Playground -- eval
        """);
}

// ---- Helpers ----------------------------------------------------------------------------------------------------

// The chunk's text on one line, shortened, for display.
static string Preview(string text) => Truncate(text.ReplaceLineEndings(" "), 110);

static string Truncate(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "...";

// Walks up from the build output folder to the folder containing the solution file, so the playground finds
// samples/, evals/ and data/ whichever directory it's run from.
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !dir.EnumerateFiles("*.slnx").Any())
    {
        dir = dir.Parent;
    }
    return dir?.FullName ?? throw new DirectoryNotFoundException("Couldn't find the repository root (no .slnx file found).");
}
