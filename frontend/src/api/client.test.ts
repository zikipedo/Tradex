import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError, endSession, fetchProtectedImage, session } from './client'

const SESSION = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2026-11-26T12:15:00Z',
  refreshTokenExpiresAt: '2026-12-03T12:00:00Z',
}

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

describe('client API', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    session.clear()
    localStorage.clear()
    fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
  })

  it('préfixe /api/v1, sérialise le corps et ignore les paramètres vides', async () => {
    fetchMock.mockResolvedValueOnce(json(200, { ok: true }))
    await api('/listings', { method: 'POST', body: { a: 1 }, query: { q: 'vélo', page: 1, city: '', type: undefined } })
    const [url, init] = fetchMock.mock.calls[0]!
    expect(url).toBe('/api/v1/listings?q=v%C3%A9lo&page=1')
    expect(init.body).toBe('{"a":1}')
    expect(init.headers['Content-Type']).toBe('application/json')
  })

  it('envoie le jeton d’accès une fois connecté, et ne stocke aucun secret dans le navigateur', async () => {
    session.store(SESSION)
    fetchMock.mockResolvedValueOnce(json(200, {}))
    await api('/users/me')
    expect(fetchMock.mock.calls[0]![1].headers.Authorization).toBe('Bearer access-1')
    // Seul un indice sans valeur est gardé ; le refresh token vit dans un cookie HttpOnly illisible en JS.
    expect(localStorage.getItem('tradx.session')).toBe('1')
    expect(JSON.stringify({ ...localStorage })).not.toContain('access-1')
    expect(sessionStorage.length).toBe(0)
  })

  it('traduit un ProblemDetails en ApiError avec les erreurs par champ', async () => {
    fetchMock.mockResolvedValueOnce(
      json(400, { title: 'Requête invalide', detail: 'Certains champs sont invalides.', code: 'validation', errors: { Title: ['Trop court.'] } }),
    )
    const error = await api('/listings', { method: 'POST', body: {} }).catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 400, message: 'Certains champs sont invalides.', code: 'validation' })
    expect((error as ApiError).fieldError('title')).toBe('Trop court.')
  })

  it('donne un message clair quand la limite de débit est atteinte', async () => {
    fetchMock.mockResolvedValueOnce(new Response('', { status: 429 }))
    await expect(api('/auth/login', { method: 'POST', body: {} })).rejects.toThrow('Trop de tentatives')
  })

  it('sur un 401, renouvelle la session par le cookie (sans corps) puis rejoue la requête une seule fois', async () => {
    session.store(SESSION)
    fetchMock
      .mockResolvedValueOnce(json(401, {}))
      .mockResolvedValueOnce(json(200, { ...SESSION, accessToken: 'access-2' }))
      .mockResolvedValueOnce(json(200, { id: 'me' }))

    await expect(api('/users/me')).resolves.toEqual({ id: 'me' })
    expect(fetchMock).toHaveBeenCalledTimes(3)
    const [refreshUrl, refreshInit] = fetchMock.mock.calls[1]!
    expect(refreshUrl).toBe('/api/v1/auth/refresh')
    expect(refreshInit).toMatchObject({ method: 'POST', credentials: 'same-origin' })
    expect(refreshInit.body).toBeUndefined()
    expect(fetchMock.mock.calls[2]![1].headers.Authorization).toBe('Bearer access-2')
  })

  it('plusieurs 401 simultanés ne déclenchent qu’un seul renouvellement (sinon l’API révoquerait la session)', async () => {
    session.store(SESSION)
    let refreshCalls = 0
    fetchMock.mockImplementation(async (url: string, init: RequestInit) => {
      if (url === '/api/v1/auth/refresh') {
        refreshCalls++
        await new Promise((r) => setTimeout(r, 10))
        return json(200, { ...SESSION, accessToken: 'access-2' })
      }
      const auth = (init.headers as Record<string, string>).Authorization
      return auth === 'Bearer access-2' ? json(200, { url }) : json(401, {})
    })

    const results = await Promise.all([api('/users/me'), api('/exchanges'), api('/time-account')])
    expect(results).toHaveLength(3)
    expect(refreshCalls).toBe(1)
  })

  it('utilise le verrou partagé entre onglets quand le navigateur le fournit', async () => {
    const request = vi.fn((_name: string, task: () => Promise<unknown>) => task())
    vi.stubGlobal('navigator', { ...navigator, locks: { request } })
    session.store(SESSION)
    fetchMock.mockResolvedValueOnce(json(401, {})).mockResolvedValueOnce(json(200, SESSION)).mockResolvedValueOnce(json(200, {}))

    await api('/users/me')
    expect(request).toHaveBeenCalledWith('tradx-refresh', expect.any(Function))
  })

  it('si le renouvellement est refusé, la session est fermée et l’erreur 401 remonte', async () => {
    session.store(SESSION)
    const listener = vi.fn()
    session.subscribe(listener)
    fetchMock.mockResolvedValueOnce(json(401, {})).mockResolvedValueOnce(json(401, { title: 'Session expirée' }))

    await expect(api('/users/me')).rejects.toMatchObject({ status: 401 })
    expect(localStorage.getItem('tradx.session')).toBeNull()
    expect(listener).toHaveBeenCalledWith(false)
  })

  it('une coupure réseau pendant le renouvellement ne déconnecte pas', async () => {
    session.store(SESSION)
    fetchMock.mockResolvedValueOnce(json(401, {})).mockRejectedValueOnce(new TypeError('Failed to fetch'))

    await expect(api('/users/me')).rejects.toMatchObject({ status: 401 })
    expect(localStorage.getItem('tradx.session')).toBe('1')
  })

  it('sans session, un 401 ne tente aucun renouvellement', async () => {
    fetchMock.mockResolvedValueOnce(json(401, {}))
    await expect(api('/users/me')).rejects.toMatchObject({ status: 401 })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('la déconnexion appelle l’API (qui efface le cookie) et oublie la session même si le réseau échoue', async () => {
    session.store(SESSION)
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'))
    await expect(endSession()).rejects.toThrow()
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/v1/auth/logout')
    expect(session.canResume()).toBe(false)
  })

  it('une déconnexion dans un autre onglet ferme aussi la session ici', () => {
    session.store(SESSION)
    const listener = vi.fn()
    session.subscribe(listener)
    window.dispatchEvent(new StorageEvent('storage', { key: 'tradx.session', oldValue: '1', newValue: null }))
    expect(listener).toHaveBeenCalledWith(false)
  })

  it('refuse de télécharger une image hors de l’API (pas de fuite du jeton vers un autre site)', async () => {
    session.store(SESSION)
    await expect(fetchProtectedImage('https://exemple.com/piege.png')).rejects.toBeInstanceOf(ApiError)
    await expect(fetchProtectedImage('//exemple.com/api/v1/x')).rejects.toBeInstanceOf(ApiError)
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
