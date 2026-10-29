using System.Text.Json;

namespace Rag.Core.Evaluation;

/// <summary>
/// A test set for retrieval: questions, each paired with text that a good search should find.
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b> it's an exam for the search. Each question comes with its "answer key": short
/// snippets of text that should appear in the retrieved chunks. Running the exam after a change (different chunk
/// size, prefixes, model...) shows whether search got better or worse, using numbers instead of gut feeling.</para>
///
/// <para><b>Why snippets rather than chunk IDs?</b> Chunk IDs change whenever chunking changes, which would break
/// the answer key. A snippet like "GitHub Actions" stays the same, so the same exam works for any chunking.</para>
///
/// <para>The file format is JSON, for example:</para>
/// <code>
/// {
///   "description": "...",
///   "cases": [
///     { "question": "Experience with CI pipelines", "expectedSnippets": ["GitHub Actions"] }
///   ]
/// }
/// </code>
/// </remarks>
/// <param name="Description">What the set covers.</param>
/// <param name="Cases">The questions.</param>
public sealed record EvalSet(string? Description, IReadOnlyList<EvalCase> Cases)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Loads an eval set from a JSON file.</summary>
    /// <exception cref="InvalidDataException">The file isn't a valid eval set.</exception>
    public static async Task<EvalSet> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var set = await JsonSerializer.DeserializeAsync<EvalSet>(stream, JsonOptions, cancellationToken);

        if (set?.Cases is null || set.Cases.Count == 0)
        {
            throw new InvalidDataException($"'{path}' has no eval cases.");
        }
        foreach (var evalCase in set.Cases)
        {
            if (string.IsNullOrWhiteSpace(evalCase.Question) || evalCase.ExpectedSnippets is not { Count: > 0 })
            {
                throw new InvalidDataException($"Every case in '{path}' needs a question and at least one expected snippet.");
            }
        }

        return set;
    }
}

/// <summary>One exam question.</summary>
/// <param name="Question">The search query.</param>
/// <param name="ExpectedSnippets">Text that should appear in the retrieved chunks (matched ignoring upper/lower case). Each snippet is a separate thing the search should find.</param>
public sealed record EvalCase(string Question, IReadOnlyList<string> ExpectedSnippets);
