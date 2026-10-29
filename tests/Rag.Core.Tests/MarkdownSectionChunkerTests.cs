using Rag.Core.Chunking;

namespace Rag.Core.Tests;

public class MarkdownSectionChunkerTests
{
    private readonly MarkdownSectionChunker _chunker = new();

    [Fact]
    public void Each_section_becomes_a_chunk_prefixed_with_its_heading_path()
    {
        var doc = TestData.Document("cv.md", """
            # Alex
            Developer.

            ## Experience
            ### Acme
            - Built APIs

            ## Skills
            C#, SQL
            """);

        var chunks = _chunker.Chunk(doc);

        Assert.Equal(
        [
            "Alex\n\nDeveloper.",
            "Alex > Experience > Acme\n\n- Built APIs",
            "Alex > Skills\n\nC#, SQL",
        ], chunks.Select(c => c.Content));
    }

    [Fact]
    public void Chunks_get_ids_indexes_and_section_metadata()
    {
        var doc = TestData.Document("cv.md", "# A\none\n## B\ntwo");

        var chunks = _chunker.Chunk(doc);

        Assert.Equal(["cv.md#0", "cv.md#1"], chunks.Select(c => c.Id));
        Assert.Equal([0, 1], chunks.Select(c => c.Index));
        Assert.All(chunks, c => Assert.Equal("cv.md", c.DocumentId));
        Assert.Equal(["A", "A > B"], chunks.Select(c => c.Metadata["section"]));
        Assert.All(chunks, c => Assert.Equal("cv.md", c.Metadata["fileName"])); // copied from the document
    }

    [Fact]
    public void A_new_heading_replaces_headings_at_its_level_and_below()
    {
        var doc = TestData.Document("d", "# A\n## B\n### C\nc\n## D\nd");

        var sections = _chunker.Chunk(doc).Select(c => c.Metadata["section"]);

        Assert.Equal(["A > B > C", "A > D"], sections);
    }

    [Fact]
    public void Headings_with_no_text_under_them_produce_no_chunk()
    {
        var doc = TestData.Document("d", "# Title\n\n## Empty\n\n## Full\ntext");

        var chunk = Assert.Single(_chunker.Chunk(doc));

        Assert.Equal("Title > Full\n\ntext", chunk.Content);
    }

    [Fact]
    public void Text_without_headings_has_no_heading_prefix()
    {
        var doc = TestData.Document("notes.txt", "Just some notes.\n\nA second paragraph.");

        var chunk = Assert.Single(_chunker.Chunk(doc));

        Assert.Equal("Just some notes.\n\nA second paragraph.", chunk.Content);
        Assert.Equal("", chunk.Metadata["section"]);
    }

    [Fact]
    public void Hash_lines_inside_code_blocks_are_not_headings()
    {
        var doc = TestData.Document("d", "# Real\n```\n# not a heading\n```");

        var chunk = Assert.Single(_chunker.Chunk(doc));

        Assert.Equal("Real", chunk.Metadata["section"]);
        Assert.Contains("# not a heading", chunk.Content);
    }

    [Fact]
    public void Long_sections_are_split_between_bullets_and_every_chunk_fits_the_limit()
    {
        var bullets = Enumerable.Range(1, 30).Select(i => $"- Bullet point number {i} with some extra words.");
        var doc = TestData.Document("d", "# Section\n" + string.Join('\n', bullets));
        var chunker = new MarkdownSectionChunker(maxChars: 200);

        var chunks = chunker.Chunk(doc);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Content.Length <= 200, $"{c.Content.Length} chars"));
        Assert.All(chunks, c => Assert.StartsWith("Section\n\n- Bullet", c.Content));
        // Nothing is lost: every bullet appears exactly once, unbroken.
        var allText = string.Join('\n', chunks.Select(c => c.Content));
        Assert.All(Enumerable.Range(1, 30), i => Assert.Contains($"number {i} with", allText));
    }

    [Fact]
    public void A_single_long_paragraph_is_split_at_sentence_ends()
    {
        var sentences = Enumerable.Range(1, 20).Select(i => $"This is sentence {i} of the paragraph.");
        var doc = TestData.Document("d", string.Join(' ', sentences));
        var chunker = new MarkdownSectionChunker(maxChars: 150);

        var chunks = chunker.Chunk(doc);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Content.Length <= 150, $"{c.Content.Length} chars"));
        Assert.All(chunks, c => Assert.EndsWith("paragraph.", c.Content));
    }

    [Fact]
    public void A_word_longer_than_the_limit_is_cut()
    {
        var doc = TestData.Document("d", new string('x', 250));
        var chunker = new MarkdownSectionChunker(maxChars: 100);

        var chunks = chunker.Chunk(doc);

        Assert.Equal([100, 100, 50], chunks.Select(c => c.Content.Length));
    }

    [Fact]
    public void Windows_line_endings_are_handled()
    {
        var doc = TestData.Document("d", "# A\r\none\r\n## B\r\ntwo");

        Assert.Equal(["A\n\none", "A > B\n\ntwo"], _chunker.Chunk(doc).Select(c => c.Content));
    }

    [Fact]
    public void Max_chars_below_100_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkdownSectionChunker(maxChars: 99));
    }
}
