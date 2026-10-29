import type { ReactNode } from 'react'
import { parseReport, type Segment, type Status } from '../report'

type Props = {
  report: string
  evidenceCount: number
  onCite: (n: number) => void
}

const statusClass: Record<Status, string> = {
  'Met': 'badge--good',
  'Partly met': 'badge--partial',
  'Not met': 'badge--bad',
}

/** The AI's match report, with a rating headline, status badges and clickable citations. */
export function Report({ report, evidenceCount, onCite }: Props) {
  const renderSegments = (segments: Segment[]): ReactNode[] =>
    segments.map((segment, i) =>
      segment.kind === 'text' ? (
        <span key={i}>{segment.text}</span>
      ) : segment.n >= 1 && segment.n <= evidenceCount ? (
        <button key={i} type="button" className="cite" onClick={() => onCite(segment.n)} title={`Show evidence ${segment.n}`}>
          {segment.n}
        </button>
      ) : (
        // A citation number with no matching evidence (the model made it up). Shown, but not clickable.
        <span key={i} className="cite cite--missing" title="No evidence with this number">{segment.n}</span>
      ),
    )

  return (
    <div className="report">
      {parseReport(report).map((line, i) => {
        switch (line.kind) {
          case 'overall':
            return (
              <div key={i} className={`overall overall--${line.rating.toLowerCase()}`}>
                <span className="overall__label">Overall match</span>
                <span className="overall__rating">{line.rating}</span>
                <p>{renderSegments(line.segments)}</p>
              </div>
            )
          case 'heading':
            return (
              <div key={i}>
                <h3 className="report__heading">{line.title}</h3>
                {line.segments.length > 0 && <p>{renderSegments(line.segments)}</p>}
              </div>
            )
          case 'requirement':
            return (
              <div key={i} className="requirement">
                <span className={`badge ${statusClass[line.status]}`}>{line.status}</span>
                <div>
                  <strong>{line.requirement}</strong>
                  {line.segments.length > 0 && <p>{renderSegments(line.segments)}</p>}
                </div>
              </div>
            )
          case 'bullet':
            return <p key={i} className="report__bullet">{renderSegments(line.segments)}</p>
          case 'paragraph':
            return <p key={i}>{renderSegments(line.segments)}</p>
        }
      })}
    </div>
  )
}
