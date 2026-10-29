import { useEffect, useState } from 'react'
import { getInfo, getSession, type Info, type Session } from './api'
import { DocumentsPanel } from './components/DocumentsPanel'
import { MatchPanel } from './components/MatchPanel'
import { SearchPanel } from './components/SearchPanel'
import { StatusBar } from './components/StatusBar'
import { useApiStatus } from './useApiStatus'

type Tab = 'match' | 'search'

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
    <div className="app">
      <header className="app__header">
        <h1>Job Match Assistant</h1>
        <p className="muted">
          Upload your CV, paste a job ad, and get a requirement-by-requirement match report where every claim cites your
          own documents.
        </p>
      </header>

      <StatusBar status={status} info={info} />

      <DocumentsPanel info={info} session={session} notice={notice} onSession={onSession} />

      <div className="step-heading step-heading--outside">
        <span className="step-number" aria-hidden="true">2</span>
        <nav className="tabs" role="tablist" aria-label="What to do with your documents">
          <button type="button" role="tab" aria-selected={tab === 'match'} className="tab" onClick={() => setTab('match')}>
            Match a job ad
          </button>
          <button type="button" role="tab" aria-selected={tab === 'search'} className="tab" onClick={() => setTab('search')}>
            Search your documents
          </button>
        </nav>
      </div>

      {/* Both panels stay mounted, so switching tabs keeps what you typed and the last results. */}
      <div hidden={tab !== 'match'}>
        <MatchPanel sessionId={session?.sessionId ?? null} onSessionExpired={onSessionExpired} />
      </div>
      <div hidden={tab !== 'search'}>
        <SearchPanel sessionId={session?.sessionId ?? null} onSessionExpired={onSessionExpired} />
      </div>

      <footer className="app__footer muted">
        Built by Shivam Missar with C#/.NET, React and retrieval-augmented generation (RAG) ·{' '}
        <a href="https://github.com/ShivamMissar/Generalised_Rag_Engine" target="_blank" rel="noreferrer">How it works, on GitHub</a>
      </footer>
    </div>
  )
}

export default App
