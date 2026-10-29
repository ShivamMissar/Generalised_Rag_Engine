import type { ReactNode } from 'react'
import { parseReport, type ReportLine, type Segment, type Status } from '../report'

type Props = {
  report: string
  evidenceCount: number
  onCite: (n: number) => void
}

const statusClass: Record<Status, string> = {
  'Met': 'good',
  'Partly met': 'partial',
  'Not met': 'bad',
}

/** The AI's match report: a rating, a meter of the verdicts, a badge per requirement, and clickable citations. */
export function Report({ report, evidenceCount, onCite }: Props) {
  const lines = parseReport(report)
  const requirements = lines.filter((l): l is Extract<ReportLine, { kind: 'requirement' }> => l.kind === 'requirement')

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
      {lines.map((line, i) => {
        switch (line.kind) {
          case 'overall':
            return (
              <div key={i} className={`overall overall--${line.rating.toLowerCase()}`}>
                <div className="overall__head">
                  <span className="overall__label">Overall match</span>
                  <span className="overall__rating">{line.rating}</span>
                </div>
                <p>{renderSegments(line.segments)}</p>
                {requirements.length > 0 && <Meter requirements={requirements} />}
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
              <div key={i} className={`requirement requirement--${statusClass[line.status]}`}>
                <span className={`badge badge--${statusClass[line.status]}`}>{line.status}</span>
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

/** One coloured segment per requirement, in report order, with a count of each verdict. */
function Meter({ requirements }: { requirements: Extract<ReportLine, { kind: 'requirement' }>[] }) {
  const count = (status: Status) => requirements.filter((r) => r.status === status).length
  const met = count('Met')
  const partly = count('Partly met')
  const notMet = count('Not met')

  return (
    <div className="meter">
      <div className="meter__bar" role="img" aria-label={`${met} met, ${partly} partly met, ${notMet} not met`}>
        {requirements.map((r, i) => (
          <span key={i} className={`meter__segment meter__segment--${statusClass[r.status]}`} title={`${r.requirement}: ${r.status}`}
            style={{ animationDelay: `${i * 70}ms` }} />
        ))}
      </div>
      <p className="meter__legend" aria-hidden="true">
        <span><i className="dot dot--good" />{met} met</span>
        {partly > 0 && <span><i className="dot dot--partial" />{partly} partly met</span>}
        <span><i className="dot dot--bad" />{notMet} not met</span>
      </p>
    </div>
  )
}
