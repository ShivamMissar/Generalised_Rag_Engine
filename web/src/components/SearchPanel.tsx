import { useState, type FormEvent } from 'react'
import { ApiError, search, type SearchResponse } from '../api'
import { EvidenceCard } from './EvidenceCard'
import { SearchIcon } from './Icons'

type Props = {
  /** The visitor's session, or null if they haven't added a CV yet. */
  sessionId: string | null
  onSessionExpired: () => void
}

/** Ask a question and see which parts of your documents match best, with their scores. */
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
    <section className="glass panel">
      <form className="search" onSubmit={onSubmit}>
        <label htmlFor="query" className="visually-hidden">Question</label>
        <SearchIcon className="icon search__icon" />
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
        <button type="submit" className="button button--primary" disabled={loading || !sessionId || query.trim() === ''}>
          {loading ? 'Searching…' : 'Search'}
        </button>
      </form>

      {!sessionId && <p className="muted">Add your CV in step 1 first.</p>}
      {error && <p className="banner banner--error">{error}</p>}

      {result && (
        <div className="search-results">
          <p className="muted">
            Best matches for “{result.query}”. Scores only mean something compared with each other: higher is closer.
          </p>
          {result.results.map((hit) => (
            <EvidenceCard key={hit.chunkId} hit={hit} />
          ))}
        </div>
      )}
    </section>
  )
}
