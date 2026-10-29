import { useRef, useState, type DragEvent } from 'react'
import { ApiError, createSampleSession, createSession, deleteSession, type Info, type Session } from '../api'

type Props = {
  info: Info | null
  session: Session | null
  /** Shown above the upload area, e.g. when the previous session expired. */
  notice: string | null
  onSession: (session: Session | null) => void
}

/** Section 01: upload a CV (or use the sample), see what was uploaded, and remove it. */
export function DocumentsPanel({ info, session, notice, onSession }: Props) {
  const input = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState<'uploading' | 'sample' | 'removing' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [dragging, setDragging] = useState(false)

  const accept = (info?.fileTypes ?? ['.pdf', '.docx', '.md', '.txt']).join(',')
  const isLocal = info?.provider.startsWith('Local') ?? false

  const run = async (kind: 'uploading' | 'sample', action: () => Promise<Session>) => {
    setBusy(kind)
    setError(null)
    try {
      onSession(await action())
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Something went wrong. Please try again.')
    } finally {
      setBusy(null)
    }
  }

  const upload = (files: FileList | null) => {
    if (!files || files.length === 0) return
    const list = Array.from(files)
    if (info && list.length > info.maxFiles) {
      setError(`Choose at most ${info.maxFiles} files.`)
      return
    }
    run('uploading', () => createSession(list))
  }

  const onDrop = (e: DragEvent) => {
    e.preventDefault()
    setDragging(false)
    if (!busy) upload(e.dataTransfer.files)
  }

  const onRemove = async () => {
    if (!session) return
    setBusy('removing')
    try {
      if (!session.isSample) await deleteSession(session.sessionId)
    } catch {
      // If the server can't be reached, its own expiry deletes the documents anyway.
    } finally {
      setBusy(null)
      onSession(null)
    }
  }

  if (session) {
    return (
      <div className="documents">
        <ul className="file-list">
          {session.documents.map((d) => (
            <li key={d.name}>
              <span className="file-list__name">{d.name}</span>
              <span className="file-list__meta">{d.chunks} {d.chunks === 1 ? 'section' : 'sections'}</span>
            </li>
          ))}
        </ul>
        <div className="documents__footer">
          <p className="small">
            {session.isSample
              ? 'Using the sample CV of “Alex Taylor”, a made-up developer.'
              : `Kept for this visit only, and deleted automatically at ${formatTime(session.expiresAt)}.`}
          </p>
          <button type="button" className="text-button text-button--danger" onClick={onRemove} disabled={busy !== null}>
            {busy === 'removing' ? 'Removing…' : session.isSample ? 'Use my own CV instead' : 'Remove my documents'}
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="documents">
      {notice && <p className="notice">{notice}</p>}

      <div
        className={`dropzone${dragging ? ' dropzone--active' : ''}`}
        onDragOver={(e) => {
          e.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
      >
        {busy === 'uploading' ? (
          <p className="dropzone__title">Reading your documents…</p>
        ) : (
          <>
            <p className="dropzone__title">Drop your CV here</p>
            <p className="small">
              PDF, Word, Markdown or plain text. Up to {info?.maxFiles ?? 3} files of {info?.maxFileMegabytes ?? 2} MB each —
              add a project write-up or two for a fuller picture.
            </p>
            <div className="dropzone__actions">
              <button type="button" className="button" onClick={() => input.current?.click()} disabled={busy !== null}>
                Choose files
              </button>
              <button type="button" className="text-button" onClick={() => run('sample', createSampleSession)} disabled={busy !== null}>
                {busy === 'sample' ? 'Loading the sample…' : 'or try a sample CV →'}
              </button>
            </div>
          </>
        )}
        <input
          ref={input}
          type="file"
          multiple
          accept={accept}
          hidden
          onChange={(e) => {
            upload(e.target.files)
            e.target.value = '' // lets the same file be chosen again
          }}
        />
      </div>

      {error && <p className="notice notice--error">{error}</p>}

      <p className="privacy">
        <strong>Privacy.</strong>{' '}
        {isLocal ? (
          <>Your files stay on this computer: they're read by local AI models and deleted after {info?.sessionMinutes ?? 60} minutes.</>
        ) : (
          <>
            Your files are sent to {info?.provider ?? 'an AI service'} to be read. This site keeps them in memory only, and
            deletes them after {info?.sessionMinutes ?? 60} minutes or when you click Remove. The AI provider's free tier may
            use what it's sent to improve its services, so take out anything you wouldn't put on a public CV — your phone
            number or home address, say — before uploading.
          </>
        )}
      </p>
    </div>
  )
}

function formatTime(iso: string | null): string {
  return iso ? new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''
}
