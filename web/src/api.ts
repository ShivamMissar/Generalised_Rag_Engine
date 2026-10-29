// Talks to the .NET API (src/Rag.Api). The types below mirror src/Rag.Api/ApiModels.cs.
//
// In development, apiBase is empty: Vite forwards /api and /health to the local API (see vite.config.ts).
// For the hosted site, VITE_API_URL is set at build time (see netlify.toml) to the API's own address, and the API
// allows this site through CORS (Rag:AllowedOrigins).
const apiBase = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

export type ChunkHit = {
  chunkId: string
  documentId: string
  section: string
  score: number
  content: string
}

/** What the server is running and its limits. */
export type Info = {
  provider: string
  chatModel: string
  sessionMinutes: number
  maxFiles: number
  maxFileMegabytes: number
  fileTypes: string[]
  remainingToday: number
}

/** A visitor's uploaded documents. expiresAt is null for the shared sample. */
export type Session = {
  sessionId: string
  documents: { name: string; chunks: number }[]
  chunks: number
  expiresAt: string | null
  isSample: boolean
}

export type SearchResponse = { query: string; results: ChunkHit[] }
export type MatchResponse = { requirements: string[]; evidence: ChunkHit[]; report: string }

/** An error with a message that's fine to show on screen. */
export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }

  /** True when the visitor's uploaded documents have expired or been removed (the API returns 404). */
  get sessionExpired(): boolean {
    return this.status === 404
  }
}

const apiDownMessage = "Can't reach the server. If the page has only just opened, it may still be waking up, so try again in a moment."

async function send(path: string, init?: RequestInit): Promise<Response> {
  let response: Response
  try {
    response = await fetch(apiBase + path, init)
  } catch {
    throw new ApiError(apiDownMessage, 0)
  }
  if (response.ok) return response

  // The API sends errors as "problem details" JSON with a readable "detail" or a list of validation "errors".
  // If the body isn't JSON, the API itself isn't answering (a proxy replied instead).
  let problem: { title?: string; detail?: string; errors?: Record<string, string[]> }
  try {
    problem = await response.json()
  } catch {
    throw new ApiError(apiDownMessage, response.status)
  }
  const validation = problem.errors ? Object.values(problem.errors).flat().join(' ') : undefined
  throw new ApiError(problem.detail ?? validation ?? problem.title ?? `Request failed (${response.status})`, response.status)
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  return (await (await send(path, init)).json()) as T
}

/** True if the API answers its health check. */
export async function checkHealth(): Promise<boolean> {
  try {
    const response = await fetch(`${apiBase}/health`)
    return response.ok && (await response.text()) === 'Healthy'
  } catch {
    return false
  }
}

export function getInfo(): Promise<Info> {
  return request('/api/info')
}

export function createSession(files: File[]): Promise<Session> {
  const form = new FormData()
  for (const file of files) form.append('files', file, file.name)
  return request('/api/sessions', { method: 'POST', body: form })
}

export function createSampleSession(): Promise<Session> {
  return request('/api/sessions/sample', { method: 'POST' })
}

export function getSession(sessionId: string): Promise<Session> {
  return request(`/api/sessions/${encodeURIComponent(sessionId)}`)
}

export async function deleteSession(sessionId: string): Promise<void> {
  await send(`/api/sessions/${encodeURIComponent(sessionId)}`, { method: 'DELETE' })
}

export function search(query: string, topK: number, sessionId: string): Promise<SearchResponse> {
  const params = new URLSearchParams({ q: query, topK: String(topK), sessionId })
  return request(`/api/search?${params}`)
}

export function matchJobAd(jobAd: string, sessionId: string): Promise<MatchResponse> {
  return request('/api/match', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ jobAd, sessionId }),
  })
}
