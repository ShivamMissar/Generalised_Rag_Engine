import { useEffect, useState } from 'react'
import { checkHealth } from './api'

/**
 * - checking: the first check hasn't finished yet
 * - waking: still waiting after a few seconds (free hosting sleeps when idle and takes up to a minute to wake)
 * - online / offline: the API answered, or it couldn't be reached
 */
export type ApiStatus = 'checking' | 'waking' | 'online' | 'offline'

/** Checks the API's health: every 2 seconds while it's not answering, every 30 seconds once it is. */
export function useApiStatus(): ApiStatus {
  const [status, setStatus] = useState<ApiStatus>('checking')

  useEffect(() => {
    let cancelled = false
    let timer: ReturnType<typeof setTimeout> | undefined
    // If the first answer takes more than 3 seconds, the server is probably waking up.
    const wakingTimer = setTimeout(() => {
      if (!cancelled) setStatus((s) => (s === 'checking' ? 'waking' : s))
    }, 3000)

    const update = async () => {
      const ok = await checkHealth()
      if (cancelled) return
      clearTimeout(wakingTimer)
      setStatus(ok ? 'online' : 'offline')
      timer = setTimeout(update, ok ? 30_000 : 2_000)
    }
    update()

    return () => {
      cancelled = true
      clearTimeout(timer)
      clearTimeout(wakingTimer)
    }
  }, [])

  return status
}
