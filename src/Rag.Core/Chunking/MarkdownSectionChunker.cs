using System.Text;
using System.Text.RegularExpressions;
using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Chunking;

/// <summary>
/// Cuts a document into chunks ("index cards") by following its Markdown headings, so each chunk covers
/// one section of the document, such as a single job on a CV.
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b> a CV is already organised into sections (Summary, Experience, each job,
/// Skills). This chunker keeps those sections together instead of cutting at random points. Each chunk
/// starts with its heading path, for example "Alex Taylor &gt; Experience &gt; Software Developer", so the
/// chunk still makes sense on its own when it's retrieved without the rest of the document.</para>
///
/// <para><b>How it works:</b></para>
/// <list type="number">
/// <item>Split the document into sections at each heading (<c>#</c>, <c>##</c>, ...), remembering the
/// heading path above each section.</item>
/// <item>Split each section into blocks: paragraphs, and individual list items (bullet points).</item>
/// <item>Pack blocks into chunks of up to <c>maxChars</c> characters. A chunk never mixes two sections,
/// and blocks are only split if a single block is too long (first at sentence ends, then at spaces).</item>
/// </list>
///
/// <para><b>Main choices:</b></para>
/// <list type="bullet">
/// <item><b>Size: about 800 characters</b> (roughly 150–200 words). Smaller chunks give more precise matches
/// but less context for the AI. Larger ones are the reverse. 800 fits a CV job entry or a project section.</item>
/// <item><b>No overlap.</b> Many chunkers repeat a bit of text between neighbouring chunks so an idea cut in
/// half isn't lost. Here we cut at natural boundaries (sections, paragraphs, bullets) and repeat the heading
/// in every chunk, so overlap would mostly add duplicate text.</item>
/// </list>
/// </remarks>
public sealed partial class MarkdownSectionChunker : IChunker
{
    /// <summary>The default maximum chunk length in characters.</summary>
    public const int DefaultMaxChars = 800;

    private readonly int _maxChars;

    /// <param name="maxChars">Target maximum length of a chunk, including its heading path. At least 100.</param>
    public MarkdownSectionChunker(int maxChars = DefaultMaxChars)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChars, 100);
        _maxChars = maxChars;
    }

    /// <inheritdoc />
    public IReadOnlyList<Chunk> Chunk(Document document)
    {
        var chunks = new List<Chunk>();

        foreach (var section in SplitIntoSections(document.Content))
        {
            // e.g. "Alex Taylor > Experience > Software Developer, Northwind Logistics (2023 to present)"
            var headingPath = string.Join(" > ", section.Headings);

            // Space left for the section's text once the heading path and the blank line after it are counted.
            // A very long heading path could leave almost no room, so always allow at least half the limit.
            var bodyBudget = headingPath.Length == 0 ? _maxChars : _maxChars - headingPath.Length - 2;
            bodyBudget = Math.Max(bodyBudget, _maxChars / 2);

            foreach (var body in PackBlocks(SplitIntoBlocks(section.Lines), bodyBudget))
            {
                // The heading path goes into the chunk's text (so it's part of the fingerprint and the AI sees
                // it) and into metadata (so code can filter or display by section).
                var content = headingPath.Length == 0 ? body : $"{headingPath}\n\n{body}";
                var metadata = new Dictionary<string, string>(document.Metadata) { ["section"] = headingPath };

                var index = chunks.Count;
                chunks.Add(new Chunk($"{document.Id}#{index}", document.Id, index, content, metadata));
            }
        }

        return chunks;
    }

    /// <summary>A run of lines under one heading, plus the path of headings above it.</summary>
    private sealed record Section(IReadOnlyList<string> Headings, List<string> Lines);

    /// <summary>A paragraph or a single list item.</summary>
    private sealed record Block(string Text, bool IsListItem);

    /// <summary>Step 1: split the text at headings, tracking the heading path.</summary>
    private static IEnumerable<Section> SplitIntoSections(string content)
    {
        // headings[0] is the current "#" heading, headings[1] the current "##" under it, and so on.
        var headings = new List<string>();
        var lines = new List<string>();
        var inCodeBlock = false;

        foreach (var rawLine in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            // Lines inside ``` code blocks can start with "#" (e.g. comments) without being headings.
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
            }

            var heading = inCodeBlock ? Match.Empty : HeadingRegex().Match(line);
            if (!heading.Success)
            {
                lines.Add(line);
                continue;
            }

            // A new heading ends the previous section.
            if (lines.Count > 0)
            {
                yield return new Section(headings.ToArray(), lines);
                lines = [];
            }

            // A heading at level N replaces the old level-N heading and everything below it. For example, a new
            // "##" heading drops the previous "##" and any "###" under it, but keeps the "#" above it.
            var level = heading.Groups[1].Length; // the number of "#" characters
            if (headings.Count >= level)
            {
                headings.RemoveRange(level - 1, headings.Count - (level - 1));
            }
            headings.Add(heading.Groups[2].Value.Trim());
        }

        if (lines.Count > 0)
        {
            yield return new Section(headings.ToArray(), lines);
        }
    }

    /// <summary>Step 2: split a section into paragraphs and list items.</summary>
    private static List<Block> SplitIntoBlocks(List<string> lines)
    {
        var blocks = new List<Block>();
        var current = new StringBuilder();
        var currentIsListItem = false;

        void Flush()
        {
            if (current.Length > 0)
            {
                blocks.Add(new Block(current.ToString().Trim(), currentIsListItem));
                current.Clear();
            }
        }

        foreach (var line in lines)
        {
            // A blank line ends a paragraph.
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
                continue;
            }

            // Each bullet point starts its own block, so packing can split a long list between bullets.
            var isListItem = ListItemRegex().IsMatch(line);
            if (isListItem)
            {
                Flush();
            }

            if (current.Length == 0)
            {
                currentIsListItem = isListItem;
            }
            else
            {
                current.Append('\n');
            }
            current.Append(line);
        }

        Flush();
        return blocks;
    }

    /// <summary>Step 3: greedily fill chunks with whole blocks, up to the budget.</summary>
    private static IEnumerable<string> PackBlocks(List<Block> blocks, int budget)
    {
        // Break up any single block that's too long to fit in a chunk by itself.
        var pieces = new List<Block>();
        foreach (var block in blocks)
        {
            if (block.Text.Length <= budget)
            {
                pieces.Add(block);
            }
            else
            {
                pieces.AddRange(SplitLongBlock(block, budget));
            }
        }

        var current = new StringBuilder();
        Block? previous = null;

        foreach (var piece in pieces)
        {
            // Keep bullet lists tight (one newline), and put a blank line between paragraphs.
            var separator = previous is { IsListItem: true } && piece.IsListItem ? "\n" : "\n\n";

            // If this piece doesn't fit, finish the current chunk and start a new one.
            if (current.Length > 0 && current.Length + separator.Length + piece.Text.Length > budget)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(separator);
            }
            current.Append(piece.Text);
            previous = piece;
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    /// <summary>Splits an over-long block at sentence ends, then regroups the sentences up to the budget.</summary>
    private static IEnumerable<Block> SplitLongBlock(Block block, int budget)
    {
        var current = new StringBuilder();

        foreach (var sentence in SentenceEndRegex().Split(block.Text))
        {
            // A single sentence longer than the budget gets cut at spaces as a last resort.
            foreach (var part in SplitAtSpaces(sentence, budget))
            {
                if (current.Length > 0 && current.Length + 1 + part.Length > budget)
                {
                    yield return new Block(current.ToString(), block.IsListItem);
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }
                current.Append(part);
            }
        }

        if (current.Length > 0)
        {
            yield return new Block(current.ToString(), block.IsListItem);
        }
    }

    /// <summary>Cuts text into pieces no longer than the budget, at the last space before the limit where possible.</summary>
    private static IEnumerable<string> SplitAtSpaces(string text, int budget)
    {
        while (text.Length > budget)
        {
            var cut = text.LastIndexOf(' ', budget);
            if (cut <= 0)
            {
                cut = budget; // No space to cut at (e.g. a very long URL), so cut mid-word.
            }

            yield return text[..cut].TrimEnd();
            text = text[cut..].TrimStart();
        }

        if (text.Length > 0)
        {
            yield return text;
        }
    }

    /// <summary>A Markdown heading: 1–6 "#" characters, a space, then the heading text.</summary>
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingRegex();

    /// <summary>A bullet ("-", "*", "+", "•") or numbered ("1.") list item.</summary>
    [GeneratedRegex(@"^\s*([-*+•]|\d+\.)\s+")]
    private static partial Regex ListItemRegex();

    /// <summary>The whitespace after a sentence-ending ".", "!" or "?".</summary>
    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEndRegex();
}
