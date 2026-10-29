type Props = { seconds: number }

// What the server is doing, roughly, at each point in a typical report. The stages are estimates based on timing (the
// API doesn't report progress), but they follow the real order of work.
const stages = [
  { from: 0, label: 'Reading the job ad and listing its requirements' },
  { from: 3, label: 'Finding the strongest evidence in your CV' },
  { from: 7, label: 'Writing the report and citing its sources' },
]

/** Shown while a match report is being prepared, which usually takes 15–30 seconds. */
export function AnalysisProgress({ seconds }: Props) {
  const current = stages.findLastIndex((s) => seconds >= s.from)

  return (
    <div className="progress" role="status" aria-live="polite">
      <div className="progress__line" aria-hidden="true"><span /></div>
      <ol className="progress__steps">
        {stages.map((stage, i) => (
          <li key={stage.label} className={i < current ? 'is-done' : i === current ? 'is-active' : ''}>
            <span className="progress__index">{String(i + 1).padStart(2, '0')}</span>
            <span>{stage.label}</span>
            <span className="progress__state">{i < current ? 'done' : i === current ? '…' : ''}</span>
          </li>
        ))}
      </ol>
      <p className="mono small">{seconds}s — usually 15 to 30</p>
    </div>
  )
}
