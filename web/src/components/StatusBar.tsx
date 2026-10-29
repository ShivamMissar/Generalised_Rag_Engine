import type { Info } from '../api'
import type { ApiStatus } from '../useApiStatus'

type Props = {
  status: ApiStatus
  info: Info | null
}

/** A small "on air" style line in the masthead: whether the server is up, and which AI it's using. */
export function StatusLine({ status, info }: Props) {
  const label = {
    checking: 'Connecting',
    waking: 'Waking up',
    offline: 'Offline',
    online: 'Live',
  }[status]

  return (
    <span className={`status status--${status}`} role="status">
      <span className="status__dot" aria-hidden="true" />
      {label}
      {status === 'online' && info && <span className="status__provider"> — {info.provider}</span>}
    </span>
  )
}

/** A notice explaining the wait while a sleeping free-tier server starts up, or why it can't be reached. */
export function StatusBanner({ status }: Pick<Props, 'status'>) {
  if (status === 'waking') {
    return (
      <p className="notice">
        <strong>Waking up the server.</strong> It runs on free hosting that sleeps when nobody is using it, so the first
        visit can take up to a minute.
      </p>
    )
  }
  if (status === 'offline') {
    return <p className="notice notice--error"><strong>Can't reach the server right now.</strong> Retrying…</p>
  }
  return null
}
