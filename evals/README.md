# Evals

An "exam" for the search half of the engine. [`retrieval.json`](retrieval.json) lists test questions about the made-up candidate in `samples/candidate`. Each question comes with **expected snippets**: short pieces of text that a good search should return.

Run it from the `src` folder:

```bash
dotnet run --project Rag.Playground -- eval
```

It indexes the sample documents, asks every question, and reports:

- **Recall@k**: how much of what we wanted was in the top k results (1.0 means all of it).
- **MRR** (mean reciprocal rank): how near the top the first good result was (1.0 means always first).
- **Hit rate@k**: the fraction of questions with at least one good result in the top k.

It runs everything twice, with and without nomic-embed-text's `search_query:` / `search_document:` prefixes, so you can compare the two settings.

## Adding a question

Add an entry to `cases` with a `question` and one or more `expectedSnippets`. Snippets are matched ignoring upper and lower case, and must appear word for word in the sample documents. A test (`EvalSetTests`) checks this, so a typo fails `dotnet test`.
