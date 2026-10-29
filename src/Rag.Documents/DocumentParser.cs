using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Rag.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using Document = Rag.Core.Models.Document;

namespace Rag.Documents;

/// <summary>
/// Turns an uploaded file (PDF, Word, Markdown or plain text) into a <see cref="Document"/> of plain text, ready
/// for chunking.
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b> most people's CVs are PDFs or Word files, which store text in complicated
/// formats. This pulls the readable text out and keeps as much of the structure as it can, because the chunker
/// uses that structure to keep related lines together:</para>
/// <list type="bullet">
/// <item><b>Word:</b> headings become Markdown headings (<c>#</c>, <c>##</c>), and list items become bullet
/// points, so a Word CV chunks exactly like a Markdown one.</item>
/// <item><b>PDF:</b> PDFs only store where each letter sits on the page. The text is grouped into blocks by
/// layout (paragraphs, headings), put into reading order, and likely headings (short lines in a bigger, bold or
/// capitalised font) are marked as Markdown headings, so a PDF CV chunks into its sections too.</item>
/// </list>
/// <para>Scanned PDFs (pictures of text) contain no text at all and are rejected with a clear message. Reading them
/// would need OCR (optical character recognition).</para>
/// </remarks>
public static class DocumentParser
{
    /// <summary>The file types that can be parsed.</summary>
    public static IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".md", ".txt" };

    /// <summary>Reads <paramref name="content"/> as the file type given by <paramref name="fileName"/>'s extension.</summary>
    /// <exception cref="DocumentParseException">The type isn't supported, the file is damaged or protected, or it contains no text.</exception>
    public static async Task<Document> ParseAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(fileName);
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            throw new DocumentParseException($"'{name}' isn't a supported file type. Upload a PDF, Word (.docx), Markdown or text file.");
        }

        // The PDF and Word readers need to jump around the file, so read it into memory first.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        string text;
        try
        {
            text = extension switch
            {
                ".pdf" => ReadPdf(buffer.ToArray()),
                ".docx" => ReadWord(buffer),
                _ => Encoding.UTF8.GetString(buffer.ToArray()),
            };
        }
        catch (Exception ex) when (ex is not DocumentParseException)
        {
            throw new DocumentParseException($"'{name}' couldn't be read. It may be damaged or password-protected.", ex);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DocumentParseException(extension == ".pdf"
                ? $"No text was found in '{name}'. It may be a scanned image, which can't be read. Try exporting it again from Word or Google Docs."
                : $"'{name}' is empty.");
        }

        var metadata = new Dictionary<string, string> { ["fileName"] = name, ["extension"] = extension };
        return new Document(name, name, text.Trim(), metadata);
    }

    /// <summary>A block of text from a PDF page, with what's needed to tell headings from body text.</summary>
    private sealed record PdfBlock(string Text, int Lines, double FontSize, bool IsBold, int Letters);

    /// <summary>
    /// Extracts the text of every page, block by block, in reading order, and marks likely headings with Markdown
    /// "#" so the chunker can split the CV into its sections.
    /// </summary>
    private static string ReadPdf(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        var blocks = new List<PdfBlock>();

        foreach (var page in pdf.GetPages())
        {
            // Letters → words → blocks of nearby lines (roughly paragraphs and headings) → reading order.
            var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
            if (words.Count == 0)
            {
                continue;
            }
            var pageBlocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            foreach (var block in UnsupervisedReadingOrderDetector.Instance.Get(pageBlocks))
            {
                blocks.AddRange(SplitByStyle(block.TextLines));
            }
        }

        return MarkHeadings(blocks);
    }

    /// <summary>
    /// Splits a block wherever the font changes between lines, and around short all-capitals lines. Layout analysis
    /// sometimes merges a heading with the text right below it (a bold job title directly above its bullet points, or
    /// "SKILLS" above the skills list in the same font), and this separates them again.
    /// </summary>
    private static IEnumerable<PdfBlock> SplitByStyle(IReadOnlyList<UglyToad.PdfPig.DocumentLayoutAnalysis.TextLine> lines)
    {
        var group = new List<(string Text, double Size, bool Bold, int Letters)>();

        foreach (var line in lines)
        {
            var letters = line.Words.SelectMany(word => word.Letters).ToList();
            var text = line.Text.Trim();
            if (letters.Count == 0 || text.Length == 0)
            {
                continue;
            }
            var size = Math.Round(letters.Average(l => l.PointSize) * 2) / 2;
            var bold = letters.Count(l => l.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true) > letters.Count / 2;

            var label = IsCapitalsLabel(text);
            if (group.Count > 0 && (label || IsCapitalsLabel(group[^1].Text) || group[^1].Size != size || group[^1].Bold != bold))
            {
                yield return ToBlock(group);
                group.Clear();
            }
            group.Add((text, size, bold, letters.Count));
        }

        if (group.Count > 0)
        {
            yield return ToBlock(group);
        }

        static PdfBlock ToBlock(List<(string Text, double Size, bool Bold, int Letters)> group) =>
            new(string.Join('\n', group.Select(l => l.Text)), group.Count, group[0].Size, group[0].Bold, group.Sum(l => l.Letters));
    }

    /// <summary>A short line in capitals, like "EXPERIENCE" or "TECHNICAL SKILLS".</summary>
    private static bool IsCapitalsLabel(string line) =>
        line.Length <= 40
        && line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 5
        && line.Count(char.IsLetter) >= 3
        && line.Where(char.IsLetter).All(char.IsUpper);

    /// <summary>
    /// PDFs don't record which lines are headings, so this infers it the way a reader does: CV headings are short
    /// single lines that are bigger than the body text, or bold, or in capitals. Each distinct heading size becomes a
    /// level, so a typical CV comes out as "# Name", "## Experience", "### Job title".
    /// </summary>
    private static string MarkHeadings(IReadOnlyList<PdfBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return "";
        }

        // The body text size is the size used by the most letters.
        var bodySize = blocks.GroupBy(b => Round(b.FontSize)).MaxBy(g => g.Sum(b => b.Letters))!.Key;

        bool IsHeading(PdfBlock b)
        {
            if (b.Lines != 1 || b.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 12)
            {
                return false;
            }
            var larger = Round(b.FontSize) >= bodySize * 1.1;
            var shortLine = b.Text.Length <= 80 && !b.Text.EndsWith('.');
            return larger || IsCapitalsLabel(b.Text) || (shortLine && b.IsBold);
        }

        // Larger headings get lower levels: the biggest size is "#", the next "##", and everything else "###".
        var headingSizes = blocks.Where(IsHeading).Select(b => Round(b.FontSize)).Distinct().OrderDescending().ToList();

        var text = new StringBuilder();
        foreach (var block in blocks)
        {
            if (IsHeading(block))
            {
                var level = Math.Min(headingSizes.IndexOf(Round(block.FontSize)) + 1, 3);
                text.Append(new string('#', level)).Append(' ');
            }
            text.Append(block.Text).Append("\n\n");
        }
        return text.ToString();

        // Sizes within half a point count as the same size.
        static double Round(double size) => Math.Round(size * 2) / 2;
    }

    /// <summary>Converts a Word document's paragraphs to Markdown: headings, bullet points and plain paragraphs.</summary>
    private static string ReadWord(Stream stream)
    {
        using var word = WordprocessingDocument.Open(stream, isEditable: false);
        var body = word.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return "";
        }

        var text = new StringBuilder();
        var previousWasListItem = false;

        // Descendants (not just direct children) also picks up paragraphs inside tables, which some CV templates use.
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var line = paragraph.InnerText.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
            var headingLevel = HeadingLevel(style);
            var isListItem = headingLevel == 0
                && (paragraph.ParagraphProperties?.NumberingProperties is not null || style.StartsWith("List", StringComparison.OrdinalIgnoreCase));

            // Bullet points sit on consecutive lines. Everything else gets a blank line before it.
            if (text.Length > 0)
            {
                text.Append(isListItem && previousWasListItem ? "\n" : "\n\n");
            }

            if (headingLevel > 0)
            {
                text.Append(new string('#', headingLevel)).Append(' ');
            }
            else if (isListItem)
            {
                text.Append("- ");
            }
            text.Append(line);
            previousWasListItem = isListItem;
        }

        return text.ToString();
    }

    /// <summary>"Title" and "Heading1" → 1, "Heading2" → 2, "Heading3" and below → 3, anything else → 0.</summary>
    private static int HeadingLevel(string style)
    {
        if (style.Equals("Title", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        if (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) && int.TryParse(style.AsSpan("Heading".Length), out var level))
        {
            return Math.Clamp(level, 1, 3);
        }
        return 0;
    }
}

/// <summary>An uploaded file couldn't be turned into text. The message is written to be shown to the person who uploaded it.</summary>
public sealed class DocumentParseException(string message, Exception? innerException = null) : Exception(message, innerException);
