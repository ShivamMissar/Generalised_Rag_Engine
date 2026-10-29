import { useEffect, useState } from 'react'
import { getInfo, getSession, type Info, type Session } from './api'
import { DocumentsPanel } from './components/DocumentsPanel'
import { GitHubIcon, SearchIcon, SparkleIcon } from './components/Icons'
import { MatchPanel } from './components/MatchPanel'
import { SearchPanel } from './components/SearchPanel'
import { StatusBanner, StatusPill } from './components/StatusBar'
import { useApiStatus } from './useApiStatus'

type Tab = 'match' | 'search'

const repoUrl = 'https://github.com/ShivamMissar/Generalised_Rag_Engine'

// The session ID is kept for this browser tab only, so a page refresh doesn't lose the uploaded CV.
const sessionKey = 'rag-session-id'

function readStoredSessionId(): string | null {
  try {
    return sessionStorage.getItem(sessionKey)
  } catch {
    return null
  }
}

function storeSessionId(id: string | null) {
  try {
    if (id) sessionStorage.setItem(sessionKey, id)
    else sessionStorage.removeItem(sessionKey)
  } catch {
    // Storage can be unavailable (e.g. some private browsing modes). The page still works without it.
  }
}

function App() {
  const status = useApiStatus()
  const [info, setInfo] = useState<Info | null>(null)
  const [session, setSession] = useState<Session | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('match')

  // Once the server is up: load its settings, and restore the session from before a refresh if it's still alive.
  useEffect(() => {
    if (status !== 'online') return
    let cancelled = false
    getInfo().then((i) => !cancelled && setInfo(i)).catch(() => {})
    const storedId = readStoredSessionId()
    if (storedId) {
      getSession(storedId)
        .then((s) => !cancelled && setSession(s))
        .catch(() => storeSessionId(null))
    }
    return () => {
      cancelled = true
    }
  }, [status])

  const onSession = (next: Session | null) => {
    setSession(next)
    setNotice(null)
    storeSessionId(next?.sessionId ?? null)
  }

  const onSessionExpired = () => {
    onSession(null)
    setNotice('Your documents expired or were removed, so please add them again.')
  }

  return (
    <>
      {/* The moving coloured light and grain behind the glass panels. Purely decorative. */}
      <div className="backdrop" aria-hidden="true">
        <span className="blob blob--1" />
        <span className="blob blob--2" />
        <span className="blob blob--3" />
        <span className="blob blob--4" />
        <span className="grain" />
      </div>

      <div className="app">
        <header className="topbar glass">
          <span className="brand">
            <span className="brand__mark" aria-hidden="true"><SparkleIcon className="icon icon--small" /></span>
            Job Match
          </span>
          <StatusPill status={status} info={info} />
          <a className="topbar__link" href={repoUrl} target="_blank" rel="noreferrer">
            <GitHubIcon className="icon icon--small" /> <span className="hide-small">Source</span>
          </a>
        </header>

        <section className="hero">
          <p className="eyebrow">Retrieval-augmented generation · C#/.NET · React</p>
          <h1>
            See how your CV <span className="gradient-text">really matches</span> the job.
          </h1>
          <p className="hero__lede">
            Upload your CV, paste a job ad, and get a requirement-by-requirement report where every claim is backed by
            a quote from your own documents.
          </p>
          <ol className="steps-strip">
            <li><span>1</span>Add your CV</li>
            <li><span>2</span>Paste a job ad</li>
            <li><span>3</span>Get a cited report</li>
          </ol>
        </section>

        <StatusBanner status={status} />

        <DocumentsPanel info={info} session={session} notice={notice} onSession={onSession} />

        <section className="step-two" aria-label="Step 2">
          <div className="step-two__header">
            <span className="step-badge" aria-hidden="true">2</span>
            <div className="segmented" role="tablist" aria-label="What to do with your documents" data-active={tab}>
              <span className="segmented__thumb" aria-hidden="true" />
              <button type="button" role="tab" aria-selected={tab === 'match'} onClick={() => setTab('match')}>
                <SparkleIcon className="icon icon--small" /> Match a job ad
              </button>
              <button type="button" role="tab" aria-selected={tab === 'search'} onClick={() => setTab('search')}>
                <SearchIcon className="icon icon--small" /> Search your documents
              </button>
            </div>
          </div>

          {/* Both panels stay mounted, so switching tabs keeps what you typed and the last results. */}
          <div hidden={tab !== 'match'}>
            <MatchPanel sessionId={session?.sessionId ?? null} onSessionExpired={onSessionExpired} />
          </div>
          <div hidden={tab !== 'search'}>
            <SearchPanel sessionId={session?.sessionId ?? null} onSessionExpired={onSessionExpired} />
          </div>
        </section>

        <footer className="footer">
          Built by Shivam Missar with C#/.NET, React and retrieval-augmented generation ·{' '}
          <a href={repoUrl} target="_blank" rel="noreferrer">How it works</a>
        </footer>
      </div>
    </>
  )
}

export default App
