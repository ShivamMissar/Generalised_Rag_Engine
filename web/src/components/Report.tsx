import type { ReactNode } from 'react'
import { parseReport, type ReportLine, type Segment, type Status } from '../report'

type Props = {
  report: string
  evidenceCount: number
  onCite: (n: number) => void
}

type Requirement = Extract<ReportLine, { kind: 'requirement' }>

// Verdicts are shown as words, not traffic-light colours: only "missing" uses the accent colour.
const statusLabel: Record<Status, string> = { 'Met': 'Met', 'Partly met': 'Partly', 'Not met': 'Missing' }
const statusClass: Record<Status, string> = { 'Met': 'met', 'Partly met': 'partly', 'Not met': 'missing' }

/** The AI's match report: a one-word verdict, a row of squares, a line per requirement, and footnote-style citations. */
export function Report({ report, evidenceCount, onCite }: Props) {
  const lines = parseReport(report)
  const requirements = lines.filter((l): l is Requirement => l.kind === 'requirement')

  const renderSegments = (segments: Segment[]): ReactNode[] =>
    segments.map((segment, i) =>
      segment.kind === 'text' ? (
        <span key={i}>{segment.text}</span>
      ) : segment.n >= 1 && segment.n <= evidenceCount ? (
        <button key={i} type="button" className="cite" onClick={() => onCite(segment.n)} title={`Show source ${segment.n}`}>
          {segment.n}
        </button>
      ) : (
        // A citation number with no matching evidence (the model made it up). Shown, but not clickable.
        <span key={i} className="cite cite--missing" title="No source with this number">{segment.n}</span>
      ),
    )

  return (
    <div className="report">
      {lines.map((line, i) => {
        switch (line.kind) {
          case 'overall':
            return (
              <div key={i} className="verdict">
                <p className="label">Overall</p>
                <p className={`verdict__word verdict__word--${line.rating.toLowerCase()}`}>{line.rating}.</p>
                {requirements.length > 0 && <Tally requirements={requirements} />}
                <p className="verdict__summary">{renderSegments(line.segments)}</p>
              </div>
            )
          case 'heading':
            // The requirement rows below speak for themselves, so the "Requirements" heading becomes a small label.
            return (
              <div key={i} className="report__block">
                <h3 className="label">{line.title}</h3>
                {line.segments.length > 0 && <p>{renderSegments(line.segments)}</p>}
              </div>
            )
          case 'requirement':
            return (
              <div key={i} className={`requirement requirement--${statusClass[line.status]}`}>
                <span className="requirement__status">{statusLabel[line.status]}</span>
                <div>
                  <p className="requirement__title">{line.requirement}</p>
                  {line.segments.length > 0 && <p className="requirement__note">{renderSegments(line.segments)}</p>}
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

/** One square per requirement, in report order: filled for met, half-filled for partly, red outline for missing. */
function Tally({ requirements }: { requirements: Requirement[] }) {
  const met = requirements.filter((r) => r.status === 'Met').length
  const partly = requirements.filter((r) => r.status === 'Partly met').length
  const missing = requirements.length - met - partly

  return (
    <div className="tally">
      <div className="tally__squares" role="img" aria-label={`${met} met, ${partly} partly met, ${missing} missing`}>
        {requirements.map((r, i) => (
          <span key={i} className={`tally__square tally__square--${statusClass[r.status]}`} title={`${r.requirement}: ${r.status}`}
            style={{ animationDelay: `${i * 60}ms` }} />
        ))}
      </div>
      <p className="mono small" aria-hidden="true">
        {met} of {requirements.length} met{partly > 0 ? ` · ${partly} partly` : ''}{missing > 0 ? ` · ${missing} missing` : ''}
      </p>
    </div>
  )
}
