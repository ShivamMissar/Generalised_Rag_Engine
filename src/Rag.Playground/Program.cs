using Microsoft.Extensions.AI;
using OllamaSharp;
using Rag.Core.Similarity;

var ollamaUri = new Uri("http://localhost:11434");

// OllamaApiClient implements the Microsoft.Extensions.AI interfaces. Code against the interfaces
// so the provider can be swapped later. (The first chat example is in git history.)
IEmbeddingGenerator<string, Embedding<float>> embedder = new OllamaApiClient(ollamaUri, "nomic-embed-text");

// Experiment: do sentences with similar meanings get similar embeddings?
string[] sentences =
[
    "Built REST APIs in C# with ASP.NET Core.",
    "Developed backend web services using .NET.",
    "Designed a PostgreSQL schema for an e-commerce site.",
    "My favourite recipe is a slow-cooked beef stew.",
    "Arsenal won the match 2-1 in extra time.",
];

const string query = "Looking for a .NET backend developer";
const string queryPrefix = "search_query: ";

// Embed every sentence in one call, as-is and again with nomic-embed-text's task prefix.
// raw[i] and prefixed[i] are both embeddings of sentences[i].
const string documentPrefix = "search_document: ";
var raw = await EmbedAsync(sentences);
var prefixed = await EmbedAsync(sentences.Select(s => documentPrefix + s));
var rawQuery = (await EmbedAsync([query]))[0];
var prefixedQuery = (await EmbedAsync([queryPrefix + query]))[0];
Console.WriteLine($"Embedded {sentences.Length} sentences, {raw[0].Length} dimensions each.\n");

// Score the query against each sentence, with and without the prefixes.
Console.WriteLine($"Query: \"{query}\"\n");
Console.WriteLine("  raw  prefixed");
for (int i = 0; i < sentences.Length; i++)
{
    float rawScore = VectorMath.CosineSimilarity(rawQuery.Span, raw[i].Span);
    float prefixedScore = VectorMath.CosineSimilarity(prefixedQuery.Span, prefixed[i].Span);
    Console.WriteLine($"{rawScore:F3}  {prefixedScore:F3}  \"{sentences[i]}\"");
}

async Task<ReadOnlyMemory<float>[]> EmbedAsync(IEnumerable<string> texts) =>
    (await embedder.GenerateAsync(texts)).Select(e => e.Vector).ToArray();
