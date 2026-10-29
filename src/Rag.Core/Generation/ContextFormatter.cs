using System.Text;
using Rag.Core.Models;

namespace Rag.Core.Generation;

/// <summary>
/// Turns retrieved chunks into a numbered "evidence" list to paste into a prompt.
/// </summary>
/// <remarks>
/// <b>In plain English:</b> before asking the AI anything, we hand it the relevant pages, each with a number and
/// where it came from:
/// <code>
/// [1] (from cv.md)
/// Alex Taylor > Experience > Software Developer ...
/// - Built and maintained REST APIs ...
/// </code>
/// The numbers let the AI cite its sources ("see [1]"), so you can check every claim against your documents.
/// </remarks>
public static class ContextFormatter
{
    /// <summary>Formats <paramref name="results"/> as evidence blocks numbered from 1, in the order given.</summary>
    public static string Format(IReadOnlyList<SearchResult> results)
    {
        var text = new StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            var chunk = results[i].Chunk;
            if (i > 0)
            {
                text.Append("\n\n");
            }
            text.Append($"[{i + 1}] (from {chunk.DocumentId})\n{chunk.Content}");
        }
        return text.ToString();
    }
}
