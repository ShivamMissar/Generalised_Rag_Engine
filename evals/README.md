# Evals

This folder will hold the retrieval evaluation set: a list of test questions (for example, "Which projects show experience with distributed systems?"), each paired with the documents or chunks it should retrieve.

Running the retriever against this set and scoring the results (for example with recall@k and MRR) shows whether changes to chunking, embeddings or retrieval actually improve quality, so the evidence isn't just a few prompts tried by hand.

Nothing is here yet. The format and scoring are planned for Weeks 5–6 (see the roadmap in the root README).
