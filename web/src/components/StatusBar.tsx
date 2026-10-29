import type { Info } from '../api'
import type { ApiStatus } from '../useApiStatus'

type Props = {
  status: ApiStatus
  info: Info | null
}

/** Whether the server is up, and which AI it's using. */
export function StatusBar({ status, info }: Props) {
  return (
    <div className="status" role="status">
      <span className={`status__dot status__dot--${status}`} aria-hidden="true" />
      <span className="status__text">
        {status === 'checking' && 'Connecting…'}
        {status === 'waking' && 'Waking up the server. The free hosting sleeps when nobody is using it, so this can take up to a minute…'}
        {status === 'offline' && "Can't reach the server right now. Retrying…"}
        {status === 'online' && (info ? <>Connected · AI: <strong>{info.provider}</strong></> : 'Connected')}
      </span>
    </div>
  )
}
