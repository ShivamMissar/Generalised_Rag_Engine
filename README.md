# Generalised RAG Engine

A general-purpose retrieval-augmented generation (RAG) engine in C#/.NET. It runs entirely on local models served by [Ollama](https://ollama.com), so no data leaves the machine.

The first use case is a **job-application assistant**: it loads my CV and project write-ups, retrieves the passages most relevant to a job ad, and uses a local LLM to analyse the match.

## Stack

- .NET 10 (LTS), C#
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) abstractions (`IChatClient`, `IEmbeddingGenerator`)
- Ollama for local models: `gemma4:12b` for chat, `nomic-embed-text` for embeddings
- SQLite for vector storage first, pgvector later
- ASP.NET Core minimal API, with a React front end later

## How it works

RAG means **looking things up before answering**. Instead of asking the AI about a candidate from memory (where it would make things up), the engine finds the relevant parts of the candidate's documents and gives them to the AI as evidence.

**1. Indexing** (once, or when documents change)

```
files ──► Loader ──► Chunker ──► Embedding model ──► Vector store
          (read)     (cut into   (fingerprint        (save in SQLite)
                      sections)   each chunk)
```

**2. Answering** (each job ad)

```
job ad ──► split into requirements ──► Retriever (best chunks for each) ──► prompt with
                                                                              numbered evidence
                                                                                    │
                       match report with citations like [2]  ◄──── chat model ◄─────┘
```

| Step | Code | In plain English |
|---|---|---|
| Load | [`TextFileLoader`](src/Rag.Core/Loading/TextFileLoader.cs) | Reads `.txt` and `.md` files into documents, skipping empty ones. |
| Chunk | [`MarkdownSectionChunker`](src/Rag.Core/Chunking/MarkdownSectionChunker.cs) | Cuts documents into ~800-character chunks along their headings, each starting with its heading path. |
| Embed and store | [`DocumentIndexer`](src/Rag.Core/Indexing/DocumentIndexer.cs), [`SqliteVectorStore`](src/Rag.Sqlite/SqliteVectorStore.cs) | Turns each chunk into a 768-number "meaning fingerprint" and saves it. |
| Compare | [`VectorMath`](src/Rag.Core/Similarity/VectorMath.cs), [`Ranking`](src/Rag.Core/Similarity/Ranking.cs) | Cosine similarity: how closely two fingerprints point the same way. |
| Retrieve | [`VectorRetriever`](src/Rag.Core/Retrieval/VectorRetriever.cs) | Fingerprints a question and returns the top-k closest chunks. |
| Generate | [`JobMatchAnalyzer`](src/Rag.Core/JobMatching/JobMatchAnalyzer.cs), [`JobMatchPrompt`](src/Rag.Core/JobMatching/JobMatchPrompt.cs) | Searches per requirement, builds the prompt and asks the chat model for a cited report. |
| Evaluate | [`RetrievalEvaluator`](src/Rag.Core/Evaluation/RetrievalEvaluator.cs), [`evals/`](evals/) | Scores search quality (recall, MRR) on a set of questions with known answers. |

Each class has comments explaining what it does, why, and the main design choices.

## Structure

```
src/
  Rag.Core/          The engine: interfaces, models, and every pipeline step above
  Rag.Sqlite/        SQLite vector store
  Rag.Playground/    Command-line tool that runs the engine (index, search, match, eval)
  Rag.Api/           ASP.NET Core minimal API (health check for now)
tests/
  Rag.Core.Tests/    Unit tests for the engine (no Ollama needed)
  Rag.Sqlite.Tests/  Tests for the SQLite store
evals/               Retrieval test questions
samples/             Made-up CV, project write-ups and job ad for testing
```

## Getting started

Make sure Ollama is running with both models pulled (`ollama pull gemma4:12b` and `ollama pull nomic-embed-text`), then, from the `src` folder:

```bash
dotnet run --project Rag.Playground -- index                    # build the index from samples/candidate
dotnet run --project Rag.Playground -- search "cloud experience"
dotnet run --project Rag.Playground -- match                    # report for samples/job-ads/backend-developer.md
dotnet run --project Rag.Playground -- eval                     # score search quality
```

Run the tests from the repository root with `dotnet test`.

Real personal documents belong in `private/`, which is git-ignored.

## Roadmap

- **Week 1:** Ollama and first chat/embedding calls.
- **Weeks 2–3:** Loading, chunking, retrieval with SQLite.
- **Week 4:** API and job-ad analysis.
- **Weeks 5–6:** Evals, tests, React front end, write-up.
