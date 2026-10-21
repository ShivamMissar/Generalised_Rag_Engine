using Microsoft.Extensions.AI;
using OllamaSharp;

var ollamaUri = new Uri("http://localhost:11434");

// OllamaApiClient implements both Microsoft.Extensions.AI interfaces. Code against the interfaces
// so the provider can be swapped later.
IChatClient chat = new OllamaApiClient(ollamaUri, "gemma4:12b");
IEmbeddingGenerator<string, Embedding<float>> embedder = new OllamaApiClient(ollamaUri, "nomic-embed-text");

// 1. Chat
var prompt = "In one sentence, what is retrieval-augmented generation?";
Console.WriteLine($"> {prompt}");
var response = await chat.GetResponseAsync(prompt);
Console.WriteLine(response.Text);
Console.WriteLine();

// 2. Embedding
var sentence = "Senior C# developer with experience building ASP.NET Core APIs.";
var vector = await embedder.GenerateVectorAsync(sentence);
Console.WriteLine($"Embedded \"{sentence}\"");
Console.WriteLine($"Dimensions: {vector.Length}");
Console.WriteLine($"First 5 values: [{string.Join(", ", vector.Span[..5].ToArray().Select(v => v.ToString("F4")))}]");
