// Turns the AI's plain-text match report into structured lines the page can style: a headline rating, status
// badges for each requirement, and clickable citations like [2].
//
// The report follows the format requested in src/Rag.Core/JobMatching/JobMatchPrompt.cs:
//
//   Overall match: Strong. One sentence why.
//   Requirements:
//   - C# and .NET: Met. Explanation [1][3].
//   Gaps: ...
//   What to highlight:
//   - ...
//
// Models don't always follow a format exactly, so anything unrecognised is still shown, as a plain paragraph.

/** A piece of text, or a citation of evidence number n. */
export type Segment = { kind: 'text'; text: string } | { kind: 'cite'; n: number }

export type Rating = 'Strong' | 'Moderate' | 'Weak'
export type Status = 'Met' | 'Partly met' | 'Not met'

export type ReportLine =
  | { kind: 'overall'; rating: Rating; segments: Segment[] }
  | { kind: 'heading'; title: string; segments: Segment[] }
  | { kind: 'requirement'; requirement: string; status: Status; segments: Segment[] }
  | { kind: 'bullet'; segments: Segment[] }
  | { kind: 'paragraph'; segments: Segment[] }

const overallPattern = /^overall match:\s*(strong|moderate|weak)\b[.:,]?\s*(.*)$/i
const headingPattern = /^(requirements|gaps|what to highlight)\s*:\s*(.*)$/i
// "- <requirement>: <status>. <explanation>". The requirement itself may contain colons.
const requirementPattern = /^[-*•]\s+(.+?):\s*(met|partly met|not met)\b[.:,]?\s*(.*)$/i
const bulletPattern = /^[-*•]\s+(.*)$/
const citationPattern = /\[(\d+)\]/g

export function parseReport(report: string): ReportLine[] {
  const lines: ReportLine[] = []

  for (const rawLine of report.split(/\r?\n/)) {
    // Models sometimes add Markdown bold (**Gaps:**). Drop it so the patterns still match.
    const line = rawLine.replaceAll('**', '').trim()
    if (line === '') continue

    let match: RegExpMatchArray | null
    if ((match = line.match(overallPattern))) {
      // "Strong as the candidate…" → the explanation reads "As the candidate…" under the rating.
      const explanation = match[2].charAt(0).toUpperCase() + match[2].slice(1)
      lines.push({ kind: 'overall', rating: capitalise(match[1]) as Rating, segments: toSegments(explanation) })
    } else if ((match = line.match(headingPattern))) {
      lines.push({ kind: 'heading', title: capitalise(match[1]), segments: toSegments(match[2]) })
    } else if ((match = line.match(requirementPattern))) {
      lines.push({
        kind: 'requirement',
        requirement: match[1].trim(),
        status: capitalise(match[2]) as Status,
        segments: toSegments(match[3]),
      })
    } else if ((match = line.match(bulletPattern))) {
      lines.push({ kind: 'bullet', segments: toSegments(match[1]) })
    } else {
      lines.push({ kind: 'paragraph', segments: toSegments(line) })
    }
  }

  return lines
}

/** Splits text into plain text and [n] citations. */
export function toSegments(text: string): Segment[] {
  const segments: Segment[] = []
  let last = 0
  for (const match of text.matchAll(citationPattern)) {
    if (match.index > last) segments.push({ kind: 'text', text: text.slice(last, match.index) })
    segments.push({ kind: 'cite', n: Number(match[1]) })
    last = match.index + match[0].length
  }
  if (last < text.length) segments.push({ kind: 'text', text: text.slice(last) })
  return segments
}

/** "partly MET" → "Partly met", "strong" → "Strong". */
function capitalise(text: string): string {
  const lower = text.toLowerCase()
  return lower.charAt(0).toUpperCase() + lower.slice(1)
}
