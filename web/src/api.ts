// Talks to the .NET API (src/Rag.Api). In development, Vite forwards /api and /health to it (see vite.config.ts).
// The types below mirror src/Rag.Api/ApiModels.cs.

export type ChunkHit = {
  chunkId: string
  documentId: string
  section: string
  score: number
  content: string
}

export type IndexResponse = { collection: string; documents: number; chunks: number }
export type SearchResponse = { query: string; results: ChunkHit[] }
export type MatchResponse = { requirements: string[]; evidence: ChunkHit[]; report: string }

/** An error with a message that's fine to show on screen. */
export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }

  /** True when nothing has been indexed yet (the API returns 409). */
  get notIndexed(): boolean {
    return this.status === 409
  }
}

const apiDownMessage = "Can't reach the API. It may still be starting. If not, stop and re-run npm run dev in the web folder."

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, init)
  } catch {
    throw new ApiError(apiDownMessage, 0)
  }

  if (response.ok) {
    return (await response.json()) as T
  }

  // The API sends errors as "problem details" JSON with a readable "detail" or a list of validation "errors".
  // If the body isn't JSON, the API itself isn't answering (the dev server's proxy replied instead).
  let problem: { title?: string; detail?: string; errors?: Record<string, string[]> }
  try {
    problem = await response.json()
  } catch {
    throw new ApiError(apiDownMessage, response.status)
  }
  const validation = problem.errors ? Object.values(problem.errors).flat().join(' ') : undefined
  throw new ApiError(problem.detail ?? validation ?? problem.title ?? `Request failed (${response.status})`, response.status)
}

/** True if the API answers its health check. */
export async function checkHealth(): Promise<boolean> {
  try {
    const response = await fetch('/health')
    return response.ok && (await response.text()) === 'Healthy'
  } catch {
    return false
  }
}

export function rebuildIndex(): Promise<IndexResponse> {
  return request('/api/index', { method: 'POST' })
}

export function search(query: string, topK: number): Promise<SearchResponse> {
  const params = new URLSearchParams({ q: query, topK: String(topK) })
  return request(`/api/search?${params}`)
}

export function matchJobAd(jobAd: string): Promise<MatchResponse> {
  return request('/api/match', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ jobAd }),
  })
}
