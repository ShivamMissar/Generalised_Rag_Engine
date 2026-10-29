using System.Text.RegularExpressions;

namespace Rag.Core.JobMatching;

/// <summary>
/// Pulls the individual requirements out of a job ad, so each one can be searched for separately.
/// </summary>
/// <remarks>
/// <para><b>Why split the ad?</b> A job ad asks for many different things (C#, SQL, testing, cloud...). One
/// fingerprint for the whole ad blends them into a vague average, so the search returns "generally relevant"
/// chunks and can miss specific skills. Searching once per requirement finds the best evidence for each.</para>
///
/// <para><b>How:</b> job ads usually list requirements as bullet points, so each bullet becomes a requirement.
/// If there are no bullets, each reasonably long sentence is used instead. This is simple and predictable. A
/// smarter (but slower and less predictable) option would be to ask the AI to extract the requirements.</para>
/// </remarks>
public static partial class JobAdParser
{
    /// <summary>The most requirements to extract, to keep the number of searches (and the prompt) manageable.</summary>
    public const int MaxRequirements = 15;

    // Sentences shorter than this (like "Apply now!") are unlikely to be requirements.
    private const int MinSentenceLength = 20;

    /// <summary>Returns the ad's bullet points, or its longer sentences if it has no bullets. At most <see cref="MaxRequirements"/>.</summary>
    public static IReadOnlyList<string> ExtractRequirements(string jobAd)
    {
        var lines = jobAd.ReplaceLineEndings("\n").Split('\n');

        var bullets = lines
            .Select(line => BulletRegex().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(text => text.Length > 0)
            .ToList();

        if (bullets.Count > 0)
        {
            return bullets.Take(MaxRequirements).ToList();
        }

        // No bullets: fall back to sentences, ignoring headings.
        var prose = string.Join(' ', lines.Where(line => !line.TrimStart().StartsWith('#')));
        return SentenceEndRegex().Split(prose)
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length >= MinSentenceLength)
            .Take(MaxRequirements)
            .ToList();
    }

    /// <summary>A bullet ("-", "*", "+", "•") or numbered ("1.") line. Group 1 is the text after the marker.</summary>
    [GeneratedRegex(@"^\s*(?:[-*+•]|\d+\.)\s+(.*)$")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEndRegex();
}
