import { CheckIcon } from './Icons'

type Props = { seconds: number }

// What the server is doing, roughly, at each point in a typical report. The stages are estimates based on timing (the
// API doesn't report progress), but they match the real order of work and give the wait a sense of movement.
const stages = [
  { from: 0, label: 'Reading the job ad and picking out its requirements' },
  { from: 3, label: 'Finding the best evidence in your documents' },
  { from: 7, label: 'Writing your report and citing the evidence' },
]

/** Shown while a match report is being prepared, which usually takes 15–30 seconds. */
export function AnalysisProgress({ seconds }: Props) {
  const current = stages.findLastIndex((s) => seconds >= s.from)

  return (
    <div className="progress glass glass--inner" role="status" aria-live="polite">
      <div className="progress__bar" aria-hidden="true"><span /></div>
      <ol className="progress__stages">
        {stages.map((stage, i) => (
          <li key={stage.label} className={i < current ? 'is-done' : i === current ? 'is-active' : ''}>
            <span className="progress__marker" aria-hidden="true">
              {i < current ? <CheckIcon className="icon icon--small" /> : i === current ? <span className="pulse-dot" /> : null}
            </span>
            {stage.label}
          </li>
        ))}
      </ol>
      <p className="muted progress__time">{seconds}s · usually 15–30 seconds</p>
    </div>
  )
}
