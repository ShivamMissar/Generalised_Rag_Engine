import type { Info } from '../api'
import type { ApiStatus } from '../useApiStatus'

type Props = {
  status: ApiStatus
  info: Info | null
}

/** A small pill in the top bar showing whether the server is up and which AI it's using. */
export function StatusPill({ status, info }: Props) {
  const label = {
    checking: 'Connecting…',
    waking: 'Waking up…',
    offline: 'Offline',
    online: 'Live',
  }[status]

  return (
    <span className={`status-pill status-pill--${status}`} role="status">
      <span className="status-pill__dot" aria-hidden="true" />
      <span>
        {label}
        {/* The provider name is left out on narrow screens to keep the top bar on one line. */}
        {status === 'online' && info && <span className="hide-small"> · {info.provider}</span>}
      </span>
    </span>
  )
}

/** A banner explaining the wait while a sleeping free-tier server starts up, or why the server can't be reached. */
export function StatusBanner({ status }: Pick<Props, 'status'>) {
  if (status === 'waking') {
    return (
      <p className="banner banner--waking">
        <span className="spinner" aria-hidden="true" />
        Waking up the server. The free hosting sleeps when nobody is using it, so this can take up to a minute…
      </p>
    )
  }
  if (status === 'offline') {
    return <p className="banner banner--offline">Can't reach the server right now. Retrying…</p>
  }
  return null
}
