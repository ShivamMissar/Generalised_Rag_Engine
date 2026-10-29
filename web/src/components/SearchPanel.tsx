import { useState, type FormEvent } from 'react'
import { ApiError, search, type SearchResponse } from '../api'
import { EvidenceCard } from './EvidenceCard'
import { Section } from './Section'

type Props = {
  /** The visitor's session, or null if they haven't added a CV yet. */
  sessionId: string | null
  onSessionExpired: () => void
}

/** Section 04: ask a question and see which parts of your documents match best, with their scores. */
export function SearchPanel({ sessionId, onSessionExpired }: Props) {
  const [query, setQuery] = useState('')
  const [topK, setTopK] = useState(5)
  const [result, setResult] = useState<SearchResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!sessionId) return
    setLoading(true)
    setError(null)
    try {
      setResult(await search(query, topK, sessionId))
    } catch (err) {
      setResult(null)
      if (err instanceof ApiError && err.sessionExpired) {
        onSessionExpired()
      } else {
        setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <Section
      number="04"
      title="Search your CV"
      aside={<p>The same search the verdict uses, on its own: type a skill or question and see which parts of your CV come closest.</p>}
    >
      <form className="search" onSubmit={onSubmit}>
        <label htmlFor="query" className="visually-hidden">Question</label>
        <input
          id="query"
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="e.g. experience with cloud deployment"
        />
        <label htmlFor="top-k" className="visually-hidden">Number of results</label>
        <select id="top-k" value={topK} onChange={(e) => setTopK(Number(e.target.value))}>
          {[3, 5, 10].map((k) => (
            <option key={k} value={k}>Top {k}</option>
          ))}
        </select>
        <button type="submit" className="button" disabled={loading || !sessionId || query.trim() === ''}>
          {loading ? 'Searching…' : 'Search'}
        </button>
      </form>

      {!sessionId && <p className="small">Add your CV in 01 first.</p>}
      {error && <p className="notice notice--error">{error}</p>}

      {result && (
        <div className="search-results">
          <p className="small">
            Closest matches for “{result.query}”. Scores only mean something next to each other: higher is closer.
          </p>
          {result.results.map((hit) => (
            <EvidenceCard key={hit.chunkId} hit={hit} />
          ))}
        </div>
      )}
    </Section>
  )
}
