import type { Session } from './types'

/**
 * Client HTTP de l'API TradX.
 *
 * Sécurité de la session :
 * - le jeton d'accès (JWT, 15 min) reste **en mémoire** uniquement ;
 * - le refresh token est dans un cookie `HttpOnly`, `Secure`, `SameSite=Strict` posé par l'API :
 *   aucun code JavaScript (ni une éventuelle faille XSS) ne peut le lire ;
 * - le navigateur ne garde qu'un **indice** sans valeur (« une session existe ») pour savoir s'il faut
 *   tenter de la restaurer au chargement, et pour que les autres onglets apprennent une déconnexion ;
 * - le renouvellement est **unique à un instant donné**, dans l'onglet et entre onglets (Web Locks) :
 *   le cookie est partagé, et l'API révoque toute la session si un refresh token déjà utilisé est présenté.
 */

const API_BASE = '/api/v1'
const SESSION_HINT_KEY = 'tradx.session'
const REFRESH_LOCK = 'tradx-refresh'

/** Erreur renvoyée par l'API au format ProblemDetails (RFC 9457). */
export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, message: string, code?: string, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.fieldErrors = fieldErrors
  }

  /** Premier message d'erreur pour un champ de formulaire (noms insensibles à la casse). */
  fieldError(field: string): string | undefined {
    const key = Object.keys(this.fieldErrors).find((k) => k.toLowerCase() === field.toLowerCase())
    return key ? this.fieldErrors[key][0] : undefined
  }
}

type Listener = (signedIn: boolean) => void

let accessToken: string | null = null
let refreshInFlight: Promise<boolean> | null = null
const listeners = new Set<Listener>()

/** L'indice de session ne contient aucun secret : seulement « 1 ». */
function hasSessionHint(): boolean {
  try {
    return localStorage.getItem(SESSION_HINT_KEY) === '1'
  } catch {
    return false
  }
}

function setSessionHint(active: boolean): void {
  try {
    if (active) localStorage.setItem(SESSION_HINT_KEY, '1')
    else localStorage.removeItem(SESSION_HINT_KEY)
  } catch {
    // Stockage indisponible (navigation privée stricte) : la session reste limitée à cet onglet.
  }
}

/** Gestion de la session côté navigateur. */
export const session = {
  /** Enregistre le jeton d'accès reçu après connexion ou renouvellement. */
  store(tokens: Session): void {
    accessToken = tokens.accessToken
    setSessionHint(true)
    listeners.forEach((l) => l(true))
  },
  /** Oublie le jeton d'accès (déconnexion ou session révoquée). Le cookie est effacé par l'API. */
  clear(): void {
    accessToken = null
    setSessionHint(false)
    listeners.forEach((l) => l(false))
  },
  /** Vrai si une session existe ou peut être restaurée depuis le cookie. */
  canResume(): boolean {
    return accessToken !== null || hasSessionHint()
  },
  /** S'abonne aux changements de connexion ; renvoie la fonction de désabonnement. */
  subscribe(listener: Listener): () => void {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
}

// Déconnexion dans un autre onglet : l'indice disparaît, on oublie aussi le jeton d'accès ici.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === SESSION_HINT_KEY && event.newValue === null && accessToken !== null) {
      accessToken = null
      listeners.forEach((l) => l(false))
    }
  })
}

/** Exécute `task` sous un verrou partagé entre onglets quand le navigateur le permet. */
function withCrossTabLock<T>(task: () => Promise<T>): Promise<T> {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
  return locks ? locks.request(REFRESH_LOCK, task) : task()
}

/**
 * Renouvelle la session à partir du cookie. Un seul renouvellement à la fois dans l'onglet
 * (promesse partagée) et entre onglets (verrou) : le suivant lit le cookie déjà renouvelé.
 */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= withCrossTabLock(async () => {
    try {
      const response = await fetch(`${API_BASE}/auth/refresh`, { method: 'POST', credentials: 'same-origin' })
      if (!response.ok) {
        if (response.status === 401) session.clear()
        return false
      }
      session.store((await response.json()) as Session)
      return true
    } catch {
      return false
    }
  }).finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

/** Déconnexion : l'API révoque la session et efface le cookie ; on oublie le jeton dans tous les cas. */
export async function endSession(): Promise<void> {
  try {
    await fetch(`${API_BASE}/auth/logout`, { method: 'POST', credentials: 'same-origin' })
  } finally {
    session.clear()
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: { title?: string; detail?: string; code?: string; errors?: Record<string, string[]> } = {}
  try {
    body = await response.json()
  } catch {
    // Réponse sans corps JSON (ex. 502 du proxy).
  }
  const message = body.detail ?? body.title ?? defaultMessage(response.status)
  return new ApiError(response.status, message, body.code, body.errors ?? {})
}

function defaultMessage(status: number): string {
  if (status === 429) return 'Trop de tentatives. Réessaie dans une minute.'
  if (status >= 500) return 'Le service ne répond pas pour le moment. Réessaie dans un instant.'
  return 'La requête n’a pas abouti.'
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  query?: Record<string, string | number | boolean | undefined | null>
  signal?: AbortSignal
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return `${API_BASE}${path}${qs ? `?${qs}` : ''}`
}

function send(path: string, options: RequestOptions): Promise<Response> {
  const isForm = options.body instanceof FormData
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (options.body !== undefined && !isForm) headers['Content-Type'] = 'application/json'
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`
  return fetch(buildUrl(path, options.query), {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : isForm ? (options.body as FormData) : JSON.stringify(options.body),
    signal: options.signal,
  })
}

/**
 * Télécharge une image protégée (photo d'un brouillon, visible par son auteur seulement) avec le jeton de session.
 * `url` est l'adresse relative renvoyée par l'API (`/api/v1/listings/{id}/photos/{photoId}`).
 */
export async function fetchProtectedImage(url: string, signal?: AbortSignal): Promise<Blob> {
  if (!url.startsWith(`${API_BASE}/`)) throw new ApiError(400, 'Adresse d’image inattendue.')
  const get = () => fetch(url, { headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : {}, signal })
  let response = await get()
  if (response.status === 401 && hasSessionHint() && (await refreshSession())) response = await get()
  if (!response.ok) throw await toApiError(response)
  return response.blob()
}

/**
 * Appelle l'API et renvoie le JSON typé.
 * Sur un 401, tente un renouvellement de session puis rejoue la requête une seule fois.
 */
export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(path, options)
  if (response.status === 401 && hasSessionHint() && (await refreshSession())) {
    response = await send(path, options)
  }
  if (!response.ok) throw await toApiError(response)
  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}
