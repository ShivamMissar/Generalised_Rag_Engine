import { useState, type FormEvent } from 'react'
import { ApiError, search, type SearchResponse } from '../api'
import { EvidenceCard } from './EvidenceCard'

/** Ask a question and see which chunks of your documents match best, with their scores. */
export function SearchPanel() {
  const [query, setQuery] = useState('')
  const [topK, setTopK] = useState(5)
  const [result, setResult] = useState<SearchResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      setResult(await search(query, topK))
    } catch (err) {
      setResult(null)
      setError(err instanceof ApiError
        ? (err.notIndexed ? 'Nothing has been indexed yet. Click "Rebuild index" at the top first.' : err.message)
        : 'Something went wrong.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <section className="panel">
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
        <button type="submit" className="button" disabled={loading || query.trim() === ''}>
          {loading ? 'Searching…' : 'Search'}
        </button>
      </form>

      {error && <p className="error">{error}</p>}

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
