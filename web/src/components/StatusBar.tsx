import { useEffect, useState } from 'react'
import { ApiError, checkHealth, rebuildIndex } from '../api'

type Health = 'checking' | 'online' | 'offline'

/** Shows whether the API is running, and lets you rebuild the index from the documents folder. */
export function StatusBar() {
  const [health, setHealth] = useState<Health>('checking')
  const [indexing, setIndexing] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    // Check now, then every 10 seconds, so the dot turns green as soon as you start the API.
    let cancelled = false
    const update = async () => {
      const ok = await checkHealth()
      if (!cancelled) setHealth(ok ? 'online' : 'offline')
    }
    update()
    const timer = setInterval(update, 10_000)
    return () => {
      cancelled = true
      clearInterval(timer)
    }
  }, [])

  const onRebuild = async () => {
    setIndexing(true)
    setMessage(null)
    try {
      const result = await rebuildIndex()
      setMessage(`Indexed ${result.documents} documents into ${result.chunks} chunks.`)
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : 'Indexing failed.')
    } finally {
      setIndexing(false)
    }
  }

  return (
    <div className="status">
      <span className={`status__dot status__dot--${health}`} aria-hidden="true" />
      <span className="status__text">
        {health === 'online' && 'API connected'}
        {health === 'offline' && <>API offline. Start it with <code>dotnet run --project src/Rag.Api</code></>}
        {health === 'checking' && 'Checking the API…'}
      </span>
      {message && <span className="status__message">{message}</span>}
      <button type="button" className="button button--secondary" onClick={onRebuild} disabled={indexing || health !== 'online'}>
        {indexing ? 'Indexing…' : 'Rebuild index'}
      </button>
    </div>
  )
}
