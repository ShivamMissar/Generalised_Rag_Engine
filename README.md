# Generalised RAG Engine

A general-purpose retrieval-augmented generation (RAG) engine in C#/.NET. It runs entirely on local models served by [Ollama](https://ollama.com), so no data leaves the machine.

The first use case is a **job-application assistant**: it loads my CV and project write-ups, retrieves the passages most relevant to a job ad, and uses a local LLM to analyse the match.

## Stack

- .NET 10 (LTS), C#
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) abstractions (`IChatClient`, `IEmbeddingGenerator`)
- Ollama for local chat and embedding models (`nomic-embed-text` for embeddings)
- SQLite for vector storage first, pgvector later
- ASP.NET Core minimal API, with a React front end later

## Structure

```
src/
  Rag.Core/          Core abstractions and models (loader, chunker, vector store, retriever)
  Rag.Playground/    Console app for experiments
  Rag.Api/           ASP.NET Core minimal API
tests/
  Rag.Core.Tests/    xUnit tests
evals/               Retrieval evaluation set
samples/             Made-up CV and project write-ups for testing
```

## Getting started

```bash
ollama serve                      # if Ollama isn't already running
dotnet build
dotnet test
dotnet run --project src/Rag.Playground
```

## Roadmap

- **Week 1:** Ollama and first chat/embedding calls.
- **Weeks 2–3:** Loading, chunking, retrieval with SQLite.
- **Week 4:** API and job-ad analysis.
- **Weeks 5–6:** Evals, tests, React front end, write-up.
