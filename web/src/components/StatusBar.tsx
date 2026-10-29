import { useEffect, useState } from 'react'
import { ApiError, checkHealth, rebuildIndex } from '../api'

type Health = 'checking' | 'online' | 'offline'

/** Shows whether the API is running, and lets you rebuild the index from the documents folder. */
export function StatusBar() {
  const [health, setHealth] = useState<Health>('checking')
  const [indexing, setIndexing] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    // Check every 2 seconds while the API is offline (it may still be starting up), then every 10 seconds once
    // it's running, so the dot turns green soon after the API is ready.
    let cancelled = false
    let timer: ReturnType<typeof setTimeout> | undefined
    const update = async () => {
      const ok = await checkHealth()
      if (cancelled) return
      setHealth(ok ? 'online' : 'offline')
      timer = setTimeout(update, ok ? 10_000 : 2_000)
    }
    update()
    return () => {
      cancelled = true
      clearTimeout(timer)
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
        {health === 'offline' && <>API offline. If you've just run <code>npm run dev</code>, it's probably still starting.</>}
        {health === 'checking' && 'Checking the API…'}
      </span>
      {message && <span className="status__message">{message}</span>}
      <button type="button" className="button button--secondary" onClick={onRebuild} disabled={indexing || health !== 'online'}>
        {indexing ? 'Indexing…' : 'Rebuild index'}
      </button>
    </div>
  )
}
