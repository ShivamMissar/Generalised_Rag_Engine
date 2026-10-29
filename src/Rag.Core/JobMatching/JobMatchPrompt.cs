using System.Text;
using Microsoft.Extensions.AI;
using Rag.Core.Generation;
using Rag.Core.Models;

namespace Rag.Core.JobMatching;

/// <summary>
/// Builds the messages sent to the chat model when assessing a job match. This is "prompt assembly": combining
/// instructions, the job ad and the retrieved evidence into one request.
/// </summary>
/// <remarks>
/// <para>The prompt has two parts:</para>
/// <list type="bullet">
/// <item><b>System message</b>: the AI's role and rules. The key rule is to use <i>only</i> the evidence, and
/// cite it. Without this, models happily invent plausible experience. That's called "hallucination", and
/// avoiding it is the whole point of RAG.</item>
/// <item><b>User message</b>: the job ad, the requirements we found in it, the numbered evidence, and the
/// exact answer format we want. A fixed format makes answers consistent and easy to read.</item>
/// </list>
/// </remarks>
public static class JobMatchPrompt
{
    /// <summary>The AI's role and ground rules.</summary>
    public const string SystemInstructions = """
        You are a careers assistant. You help a candidate judge how well they match a job ad, using evidence
        from their own CV and project write-ups.

        Rules:
        - Base every claim only on the numbered evidence provided. Never invent or assume experience.
        - Cite evidence by its number in square brackets, like [2]. A claim may cite several, like [1][3].
        - If there's no evidence for a requirement, say so plainly. Don't guess.
        - Be honest and specific. A clear "not met" is more useful than vague encouragement.
        """;

    /// <summary>Builds the system and user messages for a job-match request.</summary>
    /// <param name="jobAd">The full job ad text.</param>
    /// <param name="requirements">The requirements extracted from the ad.</param>
    /// <param name="evidence">The retrieved chunks, in the order they should be numbered.</param>
    public static IList<ChatMessage> Build(string jobAd, IReadOnlyList<string> requirements, IReadOnlyList<SearchResult> evidence)
    {
        var user = new StringBuilder();

        user.Append("Job ad:\n\"\"\"\n").Append(jobAd.Trim()).Append("\n\"\"\"\n\n");

        user.Append("Requirements found in the ad:\n");
        for (var i = 0; i < requirements.Count; i++)
        {
            user.Append($"{i + 1}. {requirements[i]}\n");
        }

        user.Append("\nEvidence from the candidate's documents:\n\n");
        user.Append(evidence.Count > 0 ? ContextFormatter.Format(evidence) : "(no evidence found)");

        user.Append("""


            Using only the evidence above, assess how well the candidate matches this job. Reply in this format:

            Overall match: Strong, Moderate or Weak, followed by one sentence explaining why.

            Requirements:
            - <requirement>: Met, Partly met or Not met. One sentence of explanation, with citations.
            (one line per requirement above, in the same order)

            Gaps: the requirements with little or no evidence, in one or two sentences.

            What to highlight: two or three specific things from the evidence the candidate should emphasise in
            their application, with citations.
            """);

        return
        [
            new ChatMessage(ChatRole.System, SystemInstructions),
            new ChatMessage(ChatRole.User, user.ToString()),
        ];
    }
}
