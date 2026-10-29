import { useEffect, useState } from 'react'
import { getInfo, getSession, type Info, type Session } from './api'
import { DocumentsPanel } from './components/DocumentsPanel'
import { MatchPanel } from './components/MatchPanel'
import { SearchPanel } from './components/SearchPanel'
import { Section } from './components/Section'
import { StatusBanner, StatusLine } from './components/StatusBar'
import { useApiStatus } from './useApiStatus'

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

  const sessionId = session?.sessionId ?? null

  return (
    <div className="page">
      <header className="masthead">
        <span className="wordmark">Job Match</span>
        <StatusLine status={status} info={info} />
        <a className="masthead__link" href={repoUrl} target="_blank" rel="noreferrer">Source ↗</a>
      </header>

      <section className="intro">
        <h1 className="intro__title">Does your CV answer the job&nbsp;ad?</h1>
        <div className="intro__text">
          <p>
            Upload your CV and paste a job ad. You get the ad broken down into its requirements, a verdict on each one,
            and the exact lines of your CV that back it up — or the honest admission that nothing does.
          </p>
          <p className="intro__note">
            I built this to check my own applications before sending them: which requirements my CV actually proves,
            and which it only hints at.
            <span className="intro__signature">Shivam Missar</span>
          </p>
        </div>
      </section>

      <StatusBanner status={status} />

      <Section number="01" title="Your CV" aside={<p>Your CV, plus any project write-ups you'd like considered.</p>}>
        <DocumentsPanel info={info} session={session} notice={notice} onSession={onSession} />
      </Section>

      <MatchPanel sessionId={sessionId} onSessionExpired={onSessionExpired} />

      <SearchPanel sessionId={sessionId} onSessionExpired={onSessionExpired} />

      <footer className="colophon">
        <p>
          Built by Shivam Missar in C#/.NET and React, using retrieval-augmented generation: the AI only sees the parts
          of your CV that a search finds relevant, and has to cite them.
        </p>
        <p className="mono">
          {info ? `AI — ${info.provider}` : 'AI — loading'} · <a href={repoUrl} target="_blank" rel="noreferrer">Source on GitHub ↗</a>
        </p>
      </footer>
    </div>
  )
}

export default App
