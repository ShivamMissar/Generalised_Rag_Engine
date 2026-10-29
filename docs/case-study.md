# Case study: a local RAG engine for job matching

**Role:** Solo project · **Stack:** C#/.NET 10, ASP.NET Core, Microsoft.Extensions.AI, Ollama, SQLite, React + TypeScript · **Code:** [this repository](../README.md)

## The problem

Applying for jobs means checking the same question again and again: *does my experience actually match what this ad asks for, and what should I emphasise?* A general chatbot can answer, but it answers from its own memory. It invents plausible experience, can't show where its claims come from, and needs your CV sent to a third party.

I wanted a tool that answers only from my own documents, shows its evidence for every claim, and runs entirely on my own machine. I also wanted to understand retrieval-augmented generation (RAG) properly by building every part of it, rather than wiring a framework together.

## What I built

A general-purpose RAG engine with a job-match assistant on top:

1. **Indexing.** CVs and project write-ups are loaded, split into chunks, turned into embeddings with `nomic-embed-text`, and stored in SQLite.
2. **Matching.** A job ad is split into its individual requirements. The engine retrieves the best evidence for each one, then asks `gemma4:12b` for a structured report: an overall rating, a Met / Partly met / Not met verdict per requirement, gaps, and what to highlight. Every claim cites numbered evidence.
3. **Interfaces.** A React page (pictured below), a REST API, and a command-line tool for experiments.

![The job-match report](images/report.png)

The engine depends only on the Microsoft.Extensions.AI abstractions. Ollama is plugged in at startup, so a hosted model could replace it without changing the engine.

## Key decisions

**Chunk along the document's structure, not by character count.** CVs are already organised into sections, so the chunker follows Markdown headings and packs paragraphs and bullet points up to about 800 characters. Each chunk starts with its heading path, such as *"Alex Taylor > Experience > Software Developer, Northwind Logistics"*, so a retrieved chunk still says whose job it describes and when. Because chunks break at natural boundaries and carry their own context, I didn't need overlap between chunks.

**Search once per requirement, not once per ad.** An embedding of a whole job ad blends C#, SQL, testing and cloud into one vague vector, so the results are "generally relevant" rather than specific. Each bullet-point requirement is searched separately.

**Give every requirement a fair share of the evidence.** Merging those results by score alone let the requirements with the most matches take every slot in the prompt. The selection now takes each requirement's single best chunk first, then fills the remaining slots by score. A unit test checks this case.

**Make the model show its work.** The system prompt limits the model to the numbered evidence, asks for a citation on every claim, and says a missing requirement should be reported plainly. In practice this works: for the sample ad, the report marks Kubernetes and HL7/FHIR as *"Not met. There is no mention of Kubernetes in the provided documents"* instead of inventing experience. The front end turns citations into links that highlight the evidence they refer to.

## Measuring instead of guessing

Two early experiments shaped the project.

**Similarity scores are relative.** When I compared sentence embeddings with cosine similarity, closely related sentences scored about 0.65, but completely unrelated ones (a beef stew recipe and a football result) still scored about 0.33, not 0. A fixed cut-off like "anything above 0.6 is a match" wouldn't work, so retrieval ranks chunks and takes the top *k* instead.

**A common tip didn't hold up.** `nomic-embed-text` is trained with `search_query:` / `search_document:` prefixes, and I expected them to separate good matches from bad ones more clearly. Applied to document-to-document comparisons, they did the opposite: every score rose and the gap between best and worst shrank. Used as intended, with a question against documents, the ranking didn't change on my small test.

That made it clear I needed a proper way to measure retrieval, so I built an **evaluation set**: questions paired with text snippets the search should return, scored with recall@k, mean reciprocal rank (MRR) and hit rate. Expected answers are snippets rather than chunk IDs, so the set stays valid when chunking changes, and a unit test fails if a snippet is ever missing from the sample documents.

| Setting (10 questions, top 3) | Recall@3 | MRR | Hit rate@3 |
|---|---|---|---|
| With prefixes | 0.85 | 0.67 | 0.90 |
| Without prefixes | 0.80 | 0.73 | 0.80 |

The result is mixed. Prefixes found more of the expected evidence, while no prefixes ranked the first good result slightly higher. I kept the model's recommended prefixes, and now any later change can be checked against the same numbers. The eval also found a concrete weakness: a question about improving performance misses the CV bullet about *cutting API response time by 40%*, because that bullet shares a chunk with three other achievements from the same job, which dilutes its embedding. That points to the next improvement, finer-grained chunks for dense sections.

## Engineering details

- **Tests that don't need the AI.** The 83 .NET tests use a fake embedding model (keyword counts) and a fake chat model, so they're fast and repeatable. The API tests run the whole app in memory with `WebApplicationFactory`, swapping in the fakes and an in-memory vector store.
- **Clear failures.** The stores reject vectors of the wrong size or from a different model, which would otherwise give meaningless scores without any error. The API returns problem-details errors: 409 "index first", and 503 "Ollama isn't running".
- **A shutdown bug found by testing.** One command (`npm run dev`) starts both the API and the web page. Testing showed that Ctrl+C stopped the page but left the API running, because `dotnet run` launches the API as a separate child process. Starting each program in its own process group and signalling the whole group fixed it, and I tested both cases: starting a new API, and reusing one that was already running.

## What I'd do next

- **pgvector**, so search uses an index inside the database instead of scanning every chunk.
- **Retrieval experiments** measured with the eval set: smaller chunks for dense sections, hybrid keyword and vector search, and a re-ranking step.
- **PDF and Word loaders** behind the existing `IDocumentLoader` interface.
- **A hosted deployment** with a hosted model provider and rate limiting. The provider-independent design is meant to allow this.

## What I learned

RAG quality is mostly decided before the model sees anything: what goes into each chunk, what's searched for, and which evidence makes it into the prompt. Building every part by hand, and measuring it, taught me far more than the generation step did. The most useful habit was treating tips and intuitions, including my own, as things to test.
