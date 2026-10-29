import { useEffect, useState } from 'react'
import { ApiError, matchJobAd, type MatchResponse } from '../api'
import sampleJobAd from '../../../samples/job-ads/backend-developer.md?raw'
import { AnalysisProgress } from './AnalysisProgress'
import { EvidenceCard } from './EvidenceCard'
import { SparkleIcon } from './Icons'
import { Report } from './Report'

type Props = {
  /** The visitor's session, or null if they haven't added a CV yet. */
  sessionId: string | null
  onSessionExpired: () => void
}

/** Paste a job ad, get the AI's match report alongside the evidence it was given. */
export function MatchPanel({ sessionId, onSessionExpired }: Props) {
  const [jobAd, setJobAd] = useState('')
  const [result, setResult] = useState<MatchResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [seconds, setSeconds] = useState(0)
  const [highlighted, setHighlighted] = useState<number | null>(null)

  // A running timer while the AI works, since a report takes 15–30 seconds.
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

  // Clicking a citation like [2] scrolls to evidence card 2 and highlights it briefly.
  const onCite = (n: number) => {
    setHighlighted(n)
    document.getElementById(`evidence-${n}`)?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
    setTimeout(() => setHighlighted((current) => (current === n ? null : current)), 2000)
  }

  return (
    <section className="glass panel">
      <div className="field">
        <div className="field__label-row">
          <label htmlFor="job-ad">Job ad</label>
          <button type="button" className="link" onClick={() => setJobAd(sampleJobAd)}>
            Use a sample job ad
          </button>
        </div>
        <textarea
          id="job-ad"
          value={jobAd}
          onChange={(e) => setJobAd(e.target.value)}
          placeholder="Paste a job ad here. Bullet-pointed requirements work best."
          rows={10}
        />
      </div>

      <div className="actions">
        <button type="button" className="button button--primary button--large" onClick={onAnalyse} disabled={loading || !sessionId || jobAd.trim() === ''}>
          <SparkleIcon className="icon icon--small" />
          {loading ? 'Analysing…' : 'Analyse match'}
        </button>
        {!sessionId && <span className="muted">Add your CV in step 1 first.</span>}
      </div>

      {loading && <AnalysisProgress seconds={seconds} />}
      {error && <p className="banner banner--error">{error}</p>}

      {result && (
        <div className="results">
          <div className="results__main">
            <h2 className="results__title">Your match report</h2>
            <p className="muted">
              Searched your documents for {result.requirements.length}{' '}
              {result.requirements.length === 1 ? 'requirement' : 'requirements'}. Click a number to see its evidence.
            </p>
            <Report report={result.report} evidenceCount={result.evidence.length} onCite={onCite} />
          </div>
          <aside className="results__side">
            <h2 className="results__title">Evidence</h2>
            <p className="muted">The parts of your documents the AI was given.</p>
            {result.evidence.map((hit, i) => (
              <EvidenceCard key={hit.chunkId} hit={hit} number={i + 1} highlighted={highlighted === i + 1} />
            ))}
          </aside>
        </div>
      )}
    </section>
  )
}
