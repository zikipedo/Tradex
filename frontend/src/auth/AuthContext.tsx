import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { refreshSession, session } from '../api/client'
import { authApi, usersApi } from '../api/endpoints'
import type { MyProfile } from '../api/types'

type Status = 'loading' | 'guest' | 'member'

interface AuthValue {
  status: Status
  me: MyProfile | null
  login: (email: string, password: string) => Promise<MyProfile>
  logout: () => Promise<void>
  /** Recharge le profil (après un échange terminé, le solde de temps a changé). */
  reloadMe: () => Promise<void>
}

const AuthContext = createContext<AuthValue | null>(null)

/** Fournit l'état de connexion à toute l'application et restaure la session au chargement. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<Status>(session.canResume() ? 'loading' : 'guest')
  const [me, setMe] = useState<MyProfile | null>(null)

  const loadMe = useCallback(async () => {
    const profile = await usersApi.me()
    setMe(profile)
    setStatus('member')
    return profile
  }, [])

  useEffect(() => {
    let cancelled = false
    if (session.canResume()) {
      refreshSession()
        .then((ok) => (ok ? loadMe() : Promise.reject()))
        .catch(() => !cancelled && setStatus('guest'))
    }
    const unsubscribe = session.subscribe((signedIn) => {
      if (!signedIn) {
        setMe(null)
        setStatus('guest')
      }
    })
    return () => {
      cancelled = true
      unsubscribe()
    }
  }, [loadMe])

  const login = useCallback(
    async (email: string, password: string) => {
      session.store(await authApi.login(email, password))
      return loadMe()
    },
    [loadMe],
  )

  const logout = useCallback(async () => {
    const token = session.refreshToken()
    try {
      if (token) await authApi.logout(token)
    } finally {
      session.clear()
    }
  }, [])

  const reloadMe = useCallback(async () => {
    if (status === 'member') await loadMe()
  }, [loadMe, status])

  const value = useMemo(() => ({ status, me, login, logout, reloadMe }), [status, me, login, logout, reloadMe])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth doit être utilisé dans <AuthProvider>.')
  return value
}
