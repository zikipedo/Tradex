import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { session } from '../../api/client'
import { AuthProvider } from '../../auth/AuthContext'
import RegisterPage from './RegisterPage'

const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status })
const PROFILE = { id: 'u1', email: 'fanta@exemple.ml', displayName: 'Fanta', role: 'User', city: 'Bamako', neighborhood: null, bio: null, timeBalanceMinutes: 0, createdAt: '' }

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/inscription']}>
      <AuthProvider>
        <Routes>
          <Route path="/inscription" element={<RegisterPage />} />
          <Route path="/annonces" element={<p>Page des annonces</p>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
  fireEvent.change(screen.getByLabelText('Ton nom affiché'), { target: { value: 'Fanta' } })
  fireEvent.change(screen.getByLabelText('Adresse e-mail'), { target: { value: 'fanta@exemple.ml' } })
  fireEvent.change(screen.getByLabelText('Mot de passe'), { target: { value: 'un-long-mot-de-passe' } })
  fireEvent.click(screen.getByRole('button', { name: 'Créer mon compte' }))
}

function mockApi(emailConfirmed: boolean, loginStatus = 200) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      if (url.endsWith('/auth/register')) return json(202, { message: 'Si cette adresse peut être utilisée…' })
      if (url.endsWith('/auth/login'))
        return loginStatus === 200 ? json(200, { accessToken: 'a', accessTokenExpiresAt: '', refreshTokenExpiresAt: '' }) : json(401, {})
      if (url.endsWith('/users/me')) return json(200, { ...PROFILE, emailConfirmed })
      return json(404, {})
    }),
  )
}

describe('RegisterPage', () => {
  beforeEach(() => session.clear())

  it('mode démonstration : connecte et mène aux annonces, sans parler d’e-mail', async () => {
    mockApi(true)
    renderPage()
    expect(await screen.findByText('Page des annonces')).toBeInTheDocument()
    expect(screen.queryByText(/boîte mail/)).not.toBeInTheDocument()
  })

  it('mode protégé : explique qu’il faut confirmer l’adresse', async () => {
    mockApi(false)
    renderPage()
    expect(await screen.findByText('Regarde ta boîte mail.')).toBeInTheDocument()
  })

  it('adresse déjà utilisée (connexion refusée) : message neutre, sans dire si le compte existe', async () => {
    mockApi(true, 401)
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent('Cette adresse a peut-être déjà un compte')
  })
})
