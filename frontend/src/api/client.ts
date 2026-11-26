import type { AuthTokens } from './types'

/**
 * Client HTTP de l'API TradX.
 *
 * Sécurité des jetons :
 * - le jeton d'accès (JWT, 15 min) reste **en mémoire** uniquement ;
 * - le refresh token est gardé dans `sessionStorage` : il survit à un rechargement de l'onglet
 *   mais disparaît à sa fermeture, et n'est jamais partagé entre onglets.
 *   (Évolution prévue : le déplacer dans un cookie HttpOnly posé par l'API.)
 * - le renouvellement est **unique à un instant donné** : l'API révoque toute la famille de jetons
 *   si un refresh token déjà utilisé est présenté, donc deux renouvellements en parallèle
 *   déconnecteraient l'utilisateur.
 */

const API_BASE = '/api/v1'
const REFRESH_KEY = 'tradx.refresh'

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

function readRefreshToken(): string | null {
  try {
    return sessionStorage.getItem(REFRESH_KEY)
  } catch {
    return null
  }
}

function writeRefreshToken(value: string | null): void {
  try {
    if (value) sessionStorage.setItem(REFRESH_KEY, value)
    else sessionStorage.removeItem(REFRESH_KEY)
  } catch {
    // Stockage indisponible (navigation privée stricte) : la session restera limitée à la mémoire.
  }
}

/** Gestion de la session côté navigateur. */
export const session = {
  /** Enregistre les jetons reçus après connexion ou renouvellement. */
  store(tokens: AuthTokens): void {
    accessToken = tokens.accessToken
    writeRefreshToken(tokens.refreshToken)
    listeners.forEach((l) => l(true))
  },
  /** Oublie tous les jetons (déconnexion ou session révoquée). */
  clear(): void {
    accessToken = null
    writeRefreshToken(null)
    listeners.forEach((l) => l(false))
  },
  /** Vrai si une session existe ou peut être restaurée. */
  canResume(): boolean {
    return accessToken !== null || readRefreshToken() !== null
  },
  refreshToken: readRefreshToken,
  /** S'abonne aux changements de connexion ; renvoie la fonction de désabonnement. */
  subscribe(listener: Listener): () => void {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
}

/** Renouvelle les jetons une seule fois même si plusieurs requêtes échouent en même temps. */
export function refreshSession(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    const refreshToken = readRefreshToken()
    if (!refreshToken) return false
    try {
      const response = await fetch(`${API_BASE}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!response.ok) {
        session.clear()
        return false
      }
      session.store((await response.json()) as AuthTokens)
      return true
    } catch {
      return false
    } finally {
      refreshInFlight = null
    }
  })()
  return refreshInFlight
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
  if (response.status === 401 && readRefreshToken() && (await refreshSession())) response = await get()
  if (!response.ok) throw await toApiError(response)
  return response.blob()
}

/**
 * Appelle l'API et renvoie le JSON typé.
 * Sur un 401, tente un renouvellement de session puis rejoue la requête une seule fois.
 */
export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(path, options)
  if (response.status === 401 && readRefreshToken() && (await refreshSession())) {
    response = await send(path, options)
  }
  if (!response.ok) throw await toApiError(response)
  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}
