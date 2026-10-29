using Rag.Core.Abstractions;
using Rag.Core.Models;

namespace Rag.Core.Loading;

/// <summary>
/// Loads plain-text and Markdown files (.txt, .md) from a folder and its subfolders, or from a single
/// file. Each file becomes one <see cref="Document"/>. Files that are empty or contain only whitespace
/// are skipped, because they would produce empty chunks with nothing to embed.
/// </summary>
public sealed class TextFileLoader : IDocumentLoader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md" };

    /// <param name="source">A folder to search recursively, or the path of a single .txt or .md file.</param>
    /// <exception cref="FileNotFoundException"><paramref name="source"/> is neither an existing folder nor an existing file.</exception>
    /// <exception cref="NotSupportedException"><paramref name="source"/> is a file with an unsupported extension.</exception>
    public async Task<IReadOnlyList<Document>> LoadAsync(string source, CancellationToken cancellationToken = default)
    {
        string root;
        IEnumerable<string> paths;

        if (Directory.Exists(source))
        {
            root = Path.GetFullPath(source);
            paths = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(IsSupported);
        }
        else if (File.Exists(source))
        {
            if (!IsSupported(source))
            {
                throw new NotSupportedException($"Unsupported file type '{Path.GetExtension(source)}'. Supported: .txt, .md.");
            }
            var fullPath = Path.GetFullPath(source);
            root = Path.GetDirectoryName(fullPath)!;
            paths = [fullPath];
        }
        else
        {
            throw new FileNotFoundException($"No file or folder found at '{source}'.", source);
        }

        var documents = new List<Document>();
        // Sort so the same folder always loads in the same order.
        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            // The path relative to the loaded folder, with forward slashes, e.g. "projects/rag-engine.md".
            var id = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            var metadata = new Dictionary<string, string>
            {
                ["fileName"] = Path.GetFileName(path),
                ["extension"] = Path.GetExtension(path).ToLowerInvariant(),
            };
            documents.Add(new Document(id, path, content, metadata));
        }

        return documents;
    }

    private static bool IsSupported(string path) => SupportedExtensions.Contains(Path.GetExtension(path));
}
