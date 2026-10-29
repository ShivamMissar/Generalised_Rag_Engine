using Rag.Core.Loading;

namespace Rag.Core.Tests;

public sealed class TextFileLoaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("rag-loader-tests-").FullName;
    private readonly TextFileLoader _loader = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Loads_txt_and_md_files_from_subfolders()
    {
        WriteFile("cv.md", "# CV");
        WriteFile("notes.txt", "Some notes");
        WriteFile("projects/app.md", "# App");

        var docs = await _loader.LoadAsync(_root);

        Assert.Equal(["cv.md", "notes.txt", "projects/app.md"], docs.Select(d => d.Id));
    }

    [Fact]
    public async Task Reads_content_and_sets_source_and_metadata()
    {
        var path = WriteFile("cv.md", "# Alex\nC# developer");

        var doc = Assert.Single(await _loader.LoadAsync(_root));

        Assert.Equal("# Alex\nC# developer", doc.Content);
        Assert.Equal(path, doc.Source);
        Assert.Equal("cv.md", doc.Metadata["fileName"]);
        Assert.Equal(".md", doc.Metadata["extension"]);
    }

    [Fact]
    public async Task Ignores_unsupported_file_types()
    {
        WriteFile("cv.md", "# CV");
        WriteFile("photo.png", "not really a png");
        WriteFile("cv.pdf", "not really a pdf");

        var doc = Assert.Single(await _loader.LoadAsync(_root));

        Assert.Equal("cv.md", doc.Id);
    }

    [Fact]
    public async Task Skips_empty_and_whitespace_only_files()
    {
        WriteFile("cv.md", "# CV");
        WriteFile("empty.md", "");
        WriteFile("blank.txt", "   \n\t  ");

        var doc = Assert.Single(await _loader.LoadAsync(_root));

        Assert.Equal("cv.md", doc.Id);
    }

    [Fact]
    public async Task Extension_matching_ignores_case()
    {
        WriteFile("CV.MD", "# CV");

        Assert.Single(await _loader.LoadAsync(_root));
    }

    [Fact]
    public async Task Loads_a_single_file()
    {
        WriteFile("other.md", "# Other");
        var path = WriteFile("cv.md", "# CV");

        var doc = Assert.Single(await _loader.LoadAsync(path));

        Assert.Equal("cv.md", doc.Id);
    }

    [Fact]
    public async Task Single_file_with_unsupported_type_throws()
    {
        var path = WriteFile("cv.pdf", "not really a pdf");

        await Assert.ThrowsAsync<NotSupportedException>(() => _loader.LoadAsync(path));
    }

    [Fact]
    public async Task Missing_path_throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _loader.LoadAsync(Path.Combine(_root, "nope")));
    }

    [Fact]
    public async Task Empty_folder_returns_no_documents()
    {
        Assert.Empty(await _loader.LoadAsync(_root));
    }
}
