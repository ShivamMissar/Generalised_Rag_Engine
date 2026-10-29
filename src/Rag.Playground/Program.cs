using Rag.Core.Loading;

// Step 1 of the pipeline: load documents from the samples folder.
// (Earlier embedding and prefix experiments are in git history.)
var samplesPath = Path.Combine(FindRepoRoot(), "samples", "candidate");

var loader = new TextFileLoader();
var documents = await loader.LoadAsync(samplesPath);

Console.WriteLine($"Loaded {documents.Count} documents from {samplesPath}\n");
foreach (var doc in documents)
{
    var firstLine = doc.Content.Split('\n')[0];
    Console.WriteLine($"{doc.Id,-30} {doc.Content.Length,6} chars   {firstLine}");
}

// Walks up from the build output folder to the folder containing the solution file, so the
// playground finds samples/ whichever directory it's run from.
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !dir.EnumerateFiles("*.slnx").Any())
    {
        dir = dir.Parent;
    }
    return dir?.FullName ?? throw new DirectoryNotFoundException("Couldn't find the repository root (no .slnx file found).");
}
