namespace Rag.Core.Indexing;

/// <summary>
/// Labels added to the front of text before it's fingerprinted. Some embedding models are trained to expect
/// a label saying whether the text is a question or a passage to be searched.
/// </summary>
/// <remarks>
/// <para><b>In plain English:</b> it's like telling the librarian "this is what I'm looking for" versus
/// "this is a book on the shelf". <c>nomic-embed-text</c> was trained with these labels, so its makers
/// recommend using them.</para>
///
/// <para><b>Important:</b> documents and queries must be fingerprinted with the matching settings. If you change
/// the prefixes, re-index the documents. Our first experiment (5 sentences) didn't show a clear difference, so
/// the <c>eval</c> command in the playground compares both settings on the eval set.</para>
/// </remarks>
/// <param name="Document">Added before each chunk's text when indexing.</param>
/// <param name="Query">Added before each question when retrieving.</param>
public sealed record EmbeddingPrefixes(string Document, string Query)
{
    /// <summary>No labels: text is fingerprinted as it is.</summary>
    public static EmbeddingPrefixes None { get; } = new("", "");

    /// <summary>The labels recommended for <c>nomic-embed-text</c>.</summary>
    public static EmbeddingPrefixes Nomic { get; } = new("search_document: ", "search_query: ");
}
