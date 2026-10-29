import { useEffect, useState } from 'react'
import { ApiError, matchJobAd, type MatchResponse } from '../api'
import sampleJobAd from '../../../samples/job-ads/backend-developer.md?raw'
import { AnalysisProgress } from './AnalysisProgress'
import { EvidenceCard } from './EvidenceCard'
import { Report } from './Report'
import { Section } from './Section'

type Props = {
  /** The visitor's session, or null if they haven't added a CV yet. */
  sessionId: string | null
  onSessionExpired: () => void
}

/** Section 02 (paste a job ad) and section 03 (the verdict, with the sources it cites). */
export function MatchPanel({ sessionId, onSessionExpired }: Props) {
  const [jobAd, setJobAd] = useState('')
  const [result, setResult] = useState<MatchResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [seconds, setSeconds] = useState(0)
  const [highlighted, setHighlighted] = useState<number | null>(null)

  // A running count while the AI works, since a report takes 15–30 seconds.
  useEffect(() => {
    if (!loading) return
    const timer = setInterval(() => setSeconds((s) => s + 1), 1000)
    return () => clearInterval(timer)
  }, [loading])

  const onAnalyse = async () => {
    if (!sessionId) return
    setSeconds(0)
    setLoading(true)
    setError(null)
    setResult(null)
    try {
      setResult(await matchJobAd(jobAd, sessionId))
      // Bring the verdict into view once it's ready.
      requestAnimationFrame(() => document.getElementById('verdict')?.scrollIntoView({ behavior: 'smooth', block: 'start' }))
    } catch (e) {
      if (e instanceof ApiError && e.sessionExpired) {
        onSessionExpired()
      } else {
        setError(e instanceof ApiError ? e.message : 'Something went wrong. Please try again.')
      }
    } finally {
      setLoading(false)
    }
  }

  // Clicking a citation like [2] scrolls to source 2 and highlights it briefly.
  const onCite = (n: number) => {
    setHighlighted(n)
    document.getElementById(`evidence-${n}`)?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
    setTimeout(() => setHighlighted((current) => (current === n ? null : current)), 2000)
  }

  return (
    <>
      <Section
        number="02"
        title="The job ad"
        aside={<p>Paste the whole ad. Bullet-pointed requirements work best, since each bullet is checked on its own.</p>}
      >
        <div className="field">
          <div className="field__row">
            <label htmlFor="job-ad" className="label">Job ad</label>
            <button type="button" className="text-button" onClick={() => setJobAd(sampleJobAd)}>
              Use a sample ad
            </button>
          </div>
          <textarea
            id="job-ad"
            value={jobAd}
            onChange={(e) => setJobAd(e.target.value)}
            placeholder="Paste a job ad here…"
            rows={11}
          />
        </div>

        <div className="actions">
          <button type="button" className="button button--accent" onClick={onAnalyse} disabled={loading || !sessionId || jobAd.trim() === ''}>
            {loading ? 'Checking…' : 'Check the match →'}
          </button>
          {!sessionId && <span className="small">Add your CV in 01 first.</span>}
        </div>

        {loading && <AnalysisProgress seconds={seconds} />}
        {error && <p className="notice notice--error">{error}</p>}
      </Section>

      {result && (
        <Section
          id="verdict"
          number="03"
          title="The verdict"
          aside={
            <p>
              {result.requirements.length} {result.requirements.length === 1 ? 'requirement' : 'requirements'} checked.
              Click a red number to see the line of your CV behind it.
            </p>
          }
        >
          <div className="results">
            <Report report={result.report} evidenceCount={result.evidence.length} onCite={onCite} />
            <aside className="sources" aria-label="Sources">
              <h3 className="label">Sources from your CV</h3>
              {result.evidence.map((hit, i) => (
                <EvidenceCard key={hit.chunkId} hit={hit} number={i + 1} highlighted={highlighted === i + 1} />
              ))}
            </aside>
          </div>
        </Section>
      )}
    </>
  )
}
