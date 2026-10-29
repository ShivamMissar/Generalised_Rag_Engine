import type { ChunkHit } from '../api'

type Props = {
  hit: ChunkHit
  /** The citation number, e.g. 2 for "[2]" in the report. Omitted for plain search results. */
  number?: number
  highlighted?: boolean
}

/** One passage from the visitor's documents, set like a footnote: number, where it's from, the text, and its score. */
export function EvidenceCard({ hit, number, highlighted = false }: Props) {
  return (
    <article
      id={number === undefined ? undefined : `evidence-${number}`}
      className={`source${highlighted ? ' source--highlighted' : ''}`}
    >
      <header className="source__header">
        {number !== undefined && <span className="source__number">{number}</span>}
        <span className="source__section">{lastSectionPart(hit.section) || hit.documentId}</span>
        <span className="source__score" title="Similarity score: higher means a closer match">{hit.score.toFixed(2)}</span>
      </header>
      <p className="source__document">{hit.documentId}</p>
      <p className="source__text">{withoutHeadingPath(hit)}</p>
    </article>
  )
}

/** "Alex Taylor > Experience > Software Developer" → "Software Developer". */
function lastSectionPart(section: string): string {
  return section.split(' > ').at(-1) ?? ''
}

/** Chunks start with their heading path (for the AI's benefit). The header shows it already, so drop it here. */
function withoutHeadingPath(hit: ChunkHit): string {
  const prefix = `${hit.section}\n\n`
  return hit.section && hit.content.startsWith(prefix) ? hit.content.slice(prefix.length) : hit.content
}
