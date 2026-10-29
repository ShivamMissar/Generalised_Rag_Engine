# Generalised RAG Engine

A general-purpose retrieval-augmented generation (RAG) engine in C#/.NET. It runs entirely on local models served by [Ollama](https://ollama.com), so no data leaves the machine.

The first use case is a **job-application assistant**: it loads my CV and project write-ups, retrieves the passages most relevant to a job ad, and uses a local LLM to analyse the match.

## Stack

- .NET 10 (LTS), C#
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) abstractions (`IChatClient`, `IEmbeddingGenerator`)
- Ollama for local models: `gemma4:12b` for chat, `nomic-embed-text` for embeddings
- SQLite for vector storage first, pgvector later
- ASP.NET Core minimal API
- React + TypeScript front end, built with Vite

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
  Rag.Api/           ASP.NET Core minimal API: index, search and match endpoints
tests/
  Rag.Core.Tests/    Unit tests for the engine (no Ollama needed)
  Rag.Sqlite.Tests/  Tests for the SQLite store
  Rag.Api.Tests/     Tests that run the API in memory with fake AI models
  Rag.Testing/       Shared fakes: a keyword "embedding model" and a canned chat model
web/                 React front end (Vite + TypeScript)
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

### The web page

The easiest way to use the engine. It needs [Node.js](https://nodejs.org) (`brew install node`) and two terminals, one for the API and one for the page:

```bash
# Terminal 1, from the repository root
dotnet run --project src/Rag.Api

# Terminal 2
cd web
npm install        # first time only
npm run dev        # then open http://localhost:5173
```

Click **Rebuild index** once, then paste a job ad (or click **Use the sample job ad**) and click **Analyse match**. The report shows a rating and a Met / Partly met / Not met badge for each requirement. Click any citation number to jump to the evidence it refers to. The **Search** tab shows which chunks best match a question.

The page is React and TypeScript, built with Vite, in [`web/`](web/). In development, Vite forwards `/api` requests to the API on port 5209. Run its tests with `npm test`.

### The API

```bash
dotnet run --project Rag.Api            # http://localhost:5209
```

| Endpoint | What it does |
|---|---|
| `GET /health` | Returns `Healthy` if the API is running. |
| `POST /api/index` | Rebuilds the index from `Rag:DocumentsPath` (default `samples/candidate`). |
| `GET /api/search?q=...&topK=5` | The best-matching chunks for a question (`topK` 1–20). |
| `POST /api/match` with `{ "jobAd": "..." }` | Requirements, evidence and the AI's match report. |

Errors come back as standard problem-details JSON: 400 for bad input, 409 if nothing is indexed yet, 503 if Ollama isn't running. Settings live in the `Rag` section of [`appsettings.json`](src/Rag.Api/appsettings.json). [`Rag.Api.http`](src/Rag.Api/Rag.Api.http) has ready-made requests for VS Code's REST Client extension, and in Development the OpenAPI description is at `/openapi/v1.json`.

Real personal documents belong in `private/`, which is git-ignored.

## Roadmap

- **Week 1:** Ollama and first chat/embedding calls.
- **Weeks 2–3:** Loading, chunking, retrieval with SQLite.
- **Week 4:** API and job-ad analysis.
- **Weeks 5–6:** Evals, tests, React front end, write-up.
