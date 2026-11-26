import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError, fetchProtectedImage, session } from './client'

const TOKENS = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2026-11-26T12:15:00Z',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2026-12-03T12:00:00Z',
}

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

describe('client API', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    session.clear()
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

  it('envoie le jeton d’accès une fois connecté, et garde le refresh token hors de la mémoire JS partagée', async () => {
    session.store(TOKENS)
    fetchMock.mockResolvedValueOnce(json(200, {}))
    await api('/users/me')
    expect(fetchMock.mock.calls[0]![1].headers.Authorization).toBe('Bearer access-1')
    expect(sessionStorage.getItem('tradx.refresh')).toBe('refresh-1')
    expect(localStorage.length).toBe(0)
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

  it('sur un 401, renouvelle la session puis rejoue la requête une seule fois', async () => {
    session.store(TOKENS)
    fetchMock
      .mockResolvedValueOnce(json(401, {}))
      .mockResolvedValueOnce(json(200, { ...TOKENS, accessToken: 'access-2', refreshToken: 'refresh-2' }))
      .mockResolvedValueOnce(json(200, { id: 'me' }))

    await expect(api('/users/me')).resolves.toEqual({ id: 'me' })
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(fetchMock.mock.calls[1]![0]).toBe('/api/v1/auth/refresh')
    expect(JSON.parse(fetchMock.mock.calls[1]![1].body)).toEqual({ refreshToken: 'refresh-1' })
    expect(fetchMock.mock.calls[2]![1].headers.Authorization).toBe('Bearer access-2')
    expect(sessionStorage.getItem('tradx.refresh')).toBe('refresh-2')
  })

  it('plusieurs 401 simultanés ne déclenchent qu’un seul renouvellement (sinon l’API révoquerait la famille de jetons)', async () => {
    session.store(TOKENS)
    let refreshCalls = 0
    fetchMock.mockImplementation(async (url: string, init: RequestInit) => {
      if (url === '/api/v1/auth/refresh') {
        refreshCalls++
        await new Promise((r) => setTimeout(r, 10))
        return json(200, { ...TOKENS, accessToken: 'access-2', refreshToken: 'refresh-2' })
      }
      const auth = (init.headers as Record<string, string>).Authorization
      return auth === 'Bearer access-2' ? json(200, { url }) : json(401, {})
    })

    const results = await Promise.all([api('/users/me'), api('/exchanges'), api('/time-account')])
    expect(results).toHaveLength(3)
    expect(refreshCalls).toBe(1)
  })

  it('si le renouvellement échoue, la session est fermée et l’erreur 401 remonte', async () => {
    session.store(TOKENS)
    const listener = vi.fn()
    session.subscribe(listener)
    fetchMock.mockResolvedValueOnce(json(401, {})).mockResolvedValueOnce(json(401, { title: 'Session expirée' }))

    await expect(api('/users/me')).rejects.toMatchObject({ status: 401 })
    expect(sessionStorage.getItem('tradx.refresh')).toBeNull()
    expect(listener).toHaveBeenCalledWith(false)
  })

  it('sans session, un 401 ne tente aucun renouvellement', async () => {
    fetchMock.mockResolvedValueOnce(json(401, {}))
    await expect(api('/users/me')).rejects.toMatchObject({ status: 401 })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('refuse de télécharger une image hors de l’API (pas de fuite du jeton vers un autre site)', async () => {
    session.store(TOKENS)
    await expect(fetchProtectedImage('https://exemple.com/piege.png')).rejects.toBeInstanceOf(ApiError)
    await expect(fetchProtectedImage('//exemple.com/api/v1/x')).rejects.toBeInstanceOf(ApiError)
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
