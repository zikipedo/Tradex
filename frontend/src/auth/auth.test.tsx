import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { session } from '../api/client'
import { AuthProvider } from './AuthContext'
import { RequireAuth } from './RequireAuth'

function renderProtected() {
  return render(
    <MemoryRouter initialEntries={['/echanges']}>
      <AuthProvider>
        <Routes>
          <Route
            path="/echanges"
            element={
              <RequireAuth>
                <p>Mes échanges</p>
              </RequireAuth>
            }
          />
          <Route path="/connexion" element={<p>Page de connexion</p>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('RequireAuth', () => {
  beforeEach(() => {
    session.clear()
    vi.stubGlobal('fetch', vi.fn())
  })

  it('redirige un visiteur vers la connexion sans appeler l’API', () => {
    renderProtected()
    expect(screen.getByText('Page de connexion')).toBeInTheDocument()
    expect(screen.queryByText('Mes échanges')).not.toBeInTheDocument()
    expect(fetch).not.toHaveBeenCalled()
  })

  it('restaure la session depuis le refresh token puis affiche la page', async () => {
    sessionStorage.setItem('tradx.refresh', 'refresh-1')
    vi.mocked(fetch).mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/auth/refresh'))
        return new Response(
          JSON.stringify({ accessToken: 'a', accessTokenExpiresAt: '', refreshToken: 'refresh-2', refreshTokenExpiresAt: '' }),
          { status: 200 },
        )
      return new Response(
        JSON.stringify({ id: 'u1', email: 'awa@exemple.ml', displayName: 'Awa', role: 'User', emailConfirmed: true, city: 'Bamako', neighborhood: null, bio: null, timeBalanceMinutes: 0, createdAt: '' }),
        { status: 200 },
      )
    })
    renderProtected()
    expect(await screen.findByText('Mes échanges')).toBeInTheDocument()
  })

  it('un refresh token révoqué renvoie vers la connexion et nettoie le stockage', async () => {
    sessionStorage.setItem('tradx.refresh', 'refresh-vole')
    vi.mocked(fetch).mockResolvedValue(new Response('{}', { status: 401 }))
    renderProtected()
    expect(await screen.findByText('Page de connexion')).toBeInTheDocument()
    expect(sessionStorage.getItem('tradx.refresh')).toBeNull()
  })
})
