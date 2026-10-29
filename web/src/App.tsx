import { useState } from 'react'
import { MatchPanel } from './components/MatchPanel'
import { SearchPanel } from './components/SearchPanel'
import { StatusBar } from './components/StatusBar'

type Tab = 'match' | 'search'

function App() {
  const [tab, setTab] = useState<Tab>('match')

  return (
    <div className="app">
      <header className="app__header">
        <div>
          <h1>Job Match Assistant</h1>
          <p className="muted">Compares a job ad with your CV and project write-ups, using local AI models.</p>
        </div>
      </header>

      <StatusBar />

      <nav className="tabs" role="tablist">
        <button type="button" role="tab" aria-selected={tab === 'match'} className="tab" onClick={() => setTab('match')}>
          Job match
        </button>
        <button type="button" role="tab" aria-selected={tab === 'search'} className="tab" onClick={() => setTab('search')}>
          Search
        </button>
      </nav>

      {/* Both panels stay mounted, so switching tabs keeps what you typed and the last results. */}
      <div hidden={tab !== 'match'}><MatchPanel /></div>
      <div hidden={tab !== 'search'}><SearchPanel /></div>
    </div>
  )
}

export default App
