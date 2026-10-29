// Starts the whole app with one command (`npm run dev`):
//   1. the .NET API (src/Rag.Api) on http://localhost:5209, unless it's already running, and
//   2. the Vite dev server for the web page on http://localhost:5173.
// Their output is shown together, labelled [api] and [web]. Ctrl+C stops both.
//
// The API port can be changed with the RAG_API_PORT environment variable. vite.config.ts reads it too.

import { spawn } from 'node:child_process'
import path from 'node:path'
import { createInterface } from 'node:readline'
import { fileURLToPath } from 'node:url'

const webDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const repoRoot = path.resolve(webDir, '..')
const apiPort = process.env.RAG_API_PORT ?? '5209'
const apiUrl = `http://localhost:${apiPort}`

const children = []
let stopping = false

function log(message) {
  console.log(`[dev] ${message}`)
}

async function apiIsHealthy() {
  try {
    const response = await fetch(`${apiUrl}/health`)
    return response.ok
  } catch {
    return false
  }
}

/**
 * Starts a program, labels each line of its output, and stops everything if it exits.
 *
 * Each program runs in its own process group ("detached"). That matters for the API: `dotnet run` launches the
 * API as a separate child process, and signalling the whole group is the reliable way to stop both.
 */
function start(name, command, args, cwd) {
  const child = spawn(command, args, {
    cwd,
    detached: true,
    stdio: ['ignore', 'pipe', 'pipe'],
    env: { ...process.env, FORCE_COLOR: '1' },
  })

  for (const stream of [child.stdout, child.stderr]) {
    createInterface({ input: stream }).on('line', (line) => console.log(`[${name}] ${line}`))
  }

  child.on('error', (error) => {
    log(`Couldn't start ${name}: ${error.message}`)
    stop(1)
  })
  child.on('exit', (code) => {
    if (!stopping) {
      log(`${name} stopped unexpectedly${code ? ` (exit code ${code})` : ''}, so stopping everything.`)
      stop(code || 1)
    }
  })

  children.push(child)
}

/** Sends a signal to a program's whole process group (a negative pid means "the group"). */
function signalGroup(child, signal) {
  try {
    process.kill(-child.pid, signal)
  } catch {
    // The group has already gone.
  }
}

/** True while any process in the program's group is still running. Signal 0 checks without sending anything. */
function groupIsRunning(child) {
  try {
    process.kill(-child.pid, 0)
    return true
  } catch {
    return false
  }
}

/**
 * Stops every program this script started, then exits. They get Ctrl+C (SIGINT) first, so they can shut down
 * cleanly, and anything still running after 5 seconds is forced to stop.
 */
function stop(exitCode = 0) {
  if (stopping) return
  stopping = true
  log('Stopping...')
  for (const child of children) signalGroup(child, 'SIGINT')

  const deadline = Date.now() + 5000
  const timer = setInterval(() => {
    const running = children.filter(groupIsRunning)
    if (running.length === 0 || Date.now() > deadline) {
      for (const child of running) signalGroup(child, 'SIGKILL')
      clearInterval(timer)
      process.exit(exitCode)
    }
  }, 200)
}

// Ctrl+C, a normal kill, or closing the terminal window.
process.on('SIGINT', () => stop(0))
process.on('SIGTERM', () => stop(0))
process.on('SIGHUP', () => stop(0))

// 1. The API: reuse it if it's already running, otherwise start it.
if (await apiIsHealthy()) {
  log(`The API is already running at ${apiUrl}, so using that one.`)
} else {
  log(`Starting the API at ${apiUrl} (the first build takes a few seconds)...`)
  start('api', 'dotnet', ['run', '--project', path.join('src', 'Rag.Api'), '--launch-profile', 'http', '--urls', apiUrl], repoRoot)

  // Report when it's ready. The page itself keeps checking too, so this is just for the terminal.
  const waitForApi = async () => {
    for (let attempt = 0; attempt < 120 && !stopping; attempt++) {
      if (await apiIsHealthy()) {
        log('The API is ready.')
        return
      }
      await new Promise((resolve) => setTimeout(resolve, 1000))
    }
  }
  waitForApi()
}

// 2. The web page.
start('web', process.execPath, [path.join(webDir, 'node_modules', 'vite', 'bin', 'vite.js'), '--strictPort'], webDir)
log('Open http://localhost:5173 once [web] says it\'s ready. Press Ctrl+C to stop.')
