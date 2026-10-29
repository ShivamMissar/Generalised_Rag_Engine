import { useRef, useState, type DragEvent } from 'react'
import { ApiError, createSampleSession, createSession, deleteSession, type Info, type Session } from '../api'
import { CheckIcon, FileIcon, LockIcon, TrashIcon, UploadIcon } from './Icons'

type Props = {
  info: Info | null
  session: Session | null
  /** Shown above the upload area, e.g. when the previous session expired. */
  notice: string | null
  onSession: (session: Session | null) => void
}

/** Step 1: upload a CV (or use the sample), see what was uploaded, and remove it. */
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

  return (
    <section className="glass panel" aria-labelledby="documents-heading">
      <header className="panel__header">
        <span className="step-badge" aria-hidden="true">1</span>
        <div>
          <h2 id="documents-heading">{session ? 'Your documents' : 'Add your CV'}</h2>
          <p className="muted">{session ? 'Ready to match against any job ad.' : 'Add project write-ups too for a fuller match.'}</p>
        </div>
      </header>

      {session ? (
        <div className="session">
          <ul className="file-chips">
            {session.documents.map((d) => (
              <li key={d.name} className="file-chip">
                <span className="file-chip__icon"><FileIcon /></span>
                <span className="file-chip__name">{d.name}</span>
                <span className="file-chip__meta"><CheckIcon className="icon icon--small" />{d.chunks} {d.chunks === 1 ? 'section' : 'sections'}</span>
              </li>
            ))}
          </ul>
          <div className="session__footer">
            <p className="muted">
              {session.isSample
                ? 'Using the sample CV of a fictional developer, "Alex Taylor".'
                : `Kept privately for this visit and deleted automatically at ${formatTime(session.expiresAt)}.`}
            </p>
            <button type="button" className="button button--ghost" onClick={onRemove} disabled={busy !== null}>
              <TrashIcon className="icon icon--small" />
              {busy === 'removing' ? 'Removing…' : session.isSample ? 'Use my own CV instead' : 'Remove my documents'}
            </button>
          </div>
        </div>
      ) : (
        <>
          {notice && <p className="banner banner--waking">{notice}</p>}

          <div
            className={`dropzone${dragging ? ' dropzone--active' : ''}${busy ? ' dropzone--busy' : ''}`}
            onDragOver={(e) => {
              e.preventDefault()
              setDragging(true)
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={onDrop}
          >
            <div className="dropzone__orb" aria-hidden="true">
              {busy === 'uploading' ? <span className="spinner spinner--large" /> : <UploadIcon className="icon icon--large" />}
            </div>
            {busy === 'uploading' ? (
              <p className="dropzone__title">Reading your documents…</p>
            ) : (
              <>
                <p className="dropzone__title">Drop your CV here</p>
                <button type="button" className="button button--primary" onClick={() => input.current?.click()} disabled={busy !== null}>
                  Choose files
                </button>
                <p className="dropzone__hint">
                  PDF, Word, Markdown or text · up to {info?.maxFiles ?? 3} files, {info?.maxFileMegabytes ?? 2} MB each
                </p>
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

          {error && <p className="banner banner--error">{error}</p>}

          <div className="panel__row">
            <p className="sample-line">
              No CV handy?{' '}
              <button type="button" className="link" onClick={() => run('sample', createSampleSession)} disabled={busy !== null}>
                {busy === 'sample' ? 'Loading the sample…' : 'Try the sample CV →'}
              </button>
            </p>
          </div>

          <p className="privacy">
            <LockIcon className="icon icon--small" />
            <span>
              {isLocal ? (
                <>Your files stay on this computer: they're read by local AI models and deleted after {info?.sessionMinutes ?? 60} minutes.</>
              ) : (
                <>
                  Your files are sent to {info?.provider ?? 'an AI service'} to be analysed. This site keeps them only in memory and
                  deletes them after {info?.sessionMinutes ?? 60} minutes, or as soon as you click Remove. The AI provider's free tier
                  may use what's sent to improve its services, so remove anything you wouldn't share publicly, such as your phone
                  number or home address, before uploading.
                </>
              )}
            </span>
          </p>
        </>
      )}
    </section>
  )
}

function formatTime(iso: string | null): string {
  return iso ? new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''
}
