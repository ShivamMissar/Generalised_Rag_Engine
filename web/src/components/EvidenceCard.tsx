import type { ChunkHit } from '../api'

type Props = {
  hit: ChunkHit
  /** The citation number, e.g. 2 for "[2]" in the report. Omitted for plain search results. */
  number?: number
  highlighted?: boolean
}

/** One chunk of the candidate's documents, with where it came from and how well it matched. */
export function EvidenceCard({ hit, number, highlighted = false }: Props) {
  return (
    <article
      id={number === undefined ? undefined : `evidence-${number}`}
      className={`evidence${highlighted ? ' evidence--highlighted' : ''}`}
    >
      <header className="evidence__header">
        {number !== undefined && <span className="evidence__number">{number}</span>}
        <div className="evidence__source">
          <span className="evidence__section">{lastSectionPart(hit.section) || hit.documentId}</span>
          <span className="evidence__document">{hit.documentId}</span>
        </div>
        <span className="evidence__score" title="Similarity score: higher means a closer match">
          {hit.score.toFixed(3)}
        </span>
      </header>
      <p className="evidence__content">{withoutHeadingPath(hit)}</p>
    </article>
  )
}

/** "Alex Taylor > Experience > Software Developer" → "Software Developer". */
function lastSectionPart(section: string): string {
  return section.split(' > ').at(-1) ?? ''
}

/** Chunks start with their heading path (for the AI's benefit). The card shows it in the header, so drop it here. */
function withoutHeadingPath(hit: ChunkHit): string {
  const prefix = `${hit.section}\n\n`
  return hit.section && hit.content.startsWith(prefix) ? hit.content.slice(prefix.length) : hit.content
}
