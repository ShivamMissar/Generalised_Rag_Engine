using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PageSize = UglyToad.PdfPig.Content.PageSize;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Rag.Documents.Tests;

public class DocumentParserTests
{
    private static Task<Rag.Core.Models.Document> Parse(string fileName, byte[] bytes) =>
        DocumentParser.ParseAsync(fileName, new MemoryStream(bytes));

    [Fact]
    public async Task Word_headings_and_lists_become_markdown()
    {
        var bytes = WordFile(
            ("Title", "Alex Taylor"),
            ("Heading1", "Experience"),
            ("", "Software developer at Northwind."),
            ("ListParagraph", "Built REST APIs in C#"),
            ("ListParagraph", "Designed PostgreSQL schemas"),
            ("Heading2", "Skills"),
            ("", "C#, SQL"));

        var doc = await Parse("cv.docx", bytes);

        Assert.Equal(
            "# Alex Taylor\n\n# Experience\n\nSoftware developer at Northwind.\n\n- Built REST APIs in C#\n- Designed PostgreSQL schemas\n\n## Skills\n\nC#, SQL",
            doc.Content);
        Assert.Equal("cv.docx", doc.Id);
        Assert.Equal(".docx", doc.Metadata["extension"]);
    }

    [Fact]
    public async Task Pdf_text_is_extracted_in_reading_order()
    {
        var bytes = PdfFile(
            ("Experience", 760),
            ("Built REST APIs in C# with ASP.NET Core.", 735),
            ("Skills", 640),
            ("C#, SQL, Docker", 615));

        var doc = await Parse("cv.pdf", bytes);

        Assert.Contains("Built REST APIs in C# with ASP.NET Core.", doc.Content);
        Assert.True(doc.Content.IndexOf("Experience", StringComparison.Ordinal) < doc.Content.IndexOf("Skills", StringComparison.Ordinal));
        Assert.Contains("C#, SQL, Docker", doc.Content);
    }

    [Fact]
    public async Task Pdf_headings_are_recognised_by_size_and_capitals()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        page.AddText("Alex Taylor", 22, new PdfPoint(50, 780), bold);
        page.AddText("Experience", 16, new PdfPoint(50, 730), bold);
        page.AddText("Software Developer, Northwind", 13, new PdfPoint(50, 700), bold);
        page.AddText("Built REST APIs in C# with ASP.NET Core for the delivery app.", 11, new PdfPoint(50, 680), regular);
        page.AddText("SKILLS", 11, new PdfPoint(50, 630), regular);
        page.AddText("C#, SQL, Docker and a long list of other tools used every day.", 11, new PdfPoint(50, 610), regular);

        var doc = await Parse("cv.pdf", builder.Build());

        Assert.Contains("# Alex Taylor\n", doc.Content);
        Assert.Contains("## Experience\n", doc.Content);
        Assert.Contains("### Software Developer, Northwind\n", doc.Content);
        Assert.Contains("### SKILLS\n", doc.Content);
        Assert.DoesNotContain("# Built REST APIs", doc.Content);
        Assert.DoesNotContain("# C#, SQL", doc.Content);
    }

    [Fact]
    public async Task Markdown_and_text_are_read_as_utf8()
    {
        var doc = await Parse("notes.md", Encoding.UTF8.GetBytes("# Café\nNaïve résumé"));

        Assert.Equal("# Café\nNaïve résumé", doc.Content);
    }

    [Fact]
    public async Task Path_parts_of_the_file_name_are_dropped()
    {
        var doc = await Parse("../../etc/cv.txt", Encoding.UTF8.GetBytes("text"));

        Assert.Equal("cv.txt", doc.Id);
    }

    [Theory]
    [InlineData("photo.png")]
    [InlineData("cv.doc")]
    [InlineData("no-extension")]
    public async Task Unsupported_types_are_rejected_with_a_helpful_message(string fileName)
    {
        var ex = await Assert.ThrowsAsync<DocumentParseException>(() => Parse(fileName, [1, 2, 3]));

        Assert.Contains("PDF, Word (.docx), Markdown or text", ex.Message);
    }

    [Fact]
    public async Task A_pdf_with_no_text_is_rejected_as_a_possible_scan()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);

        var ex = await Assert.ThrowsAsync<DocumentParseException>(() => Parse("scan.pdf", builder.Build()));

        Assert.Contains("scanned image", ex.Message);
    }

    [Theory]
    [InlineData("cv.pdf")]
    [InlineData("cv.docx")]
    public async Task Damaged_files_are_rejected_with_a_helpful_message(string fileName)
    {
        var ex = await Assert.ThrowsAsync<DocumentParseException>(() => Parse(fileName, Encoding.UTF8.GetBytes("not really a document")));

        Assert.Contains("couldn't be read", ex.Message);
    }

    [Fact]
    public async Task An_empty_text_file_is_rejected()
    {
        await Assert.ThrowsAsync<DocumentParseException>(() => Parse("cv.txt", Encoding.UTF8.GetBytes("  \n ")));
    }

    /// <summary>Builds a .docx in memory. An empty style means a normal paragraph.</summary>
    private static byte[] WordFile(params (string Style, string Text)[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();
            var body = new Body();
            foreach (var (style, text) in paragraphs)
            {
                var paragraph = new Paragraph(new Run(new Text(text)));
                if (style.Length > 0)
                {
                    paragraph.ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = style });
                }
                body.Append(paragraph);
            }
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);
        }
        return stream.ToArray();
    }

    /// <summary>Builds a one-page PDF with each line of text at the given height from the bottom of the page.</summary>
    private static byte[] PdfFile(params (string Text, double Y)[] lines)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var (text, y) in lines)
        {
            page.AddText(text, 12, new PdfPoint(50, y), font);
        }
        return builder.Build();
    }
}
