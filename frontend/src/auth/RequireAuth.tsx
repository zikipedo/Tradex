import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Loader } from '../components/Loader'
import { useAuth } from './AuthContext'

/**
 * Protège une page réservée aux membres : redirige vers la connexion en gardant la page demandée.
 * C'est un confort d'interface : la vraie protection est faite par l'API (deny by default).
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useAuth()
  const location = useLocation()
  if (status === 'loading') return <Loader label="Ouverture de ta session" />
  if (status === 'guest') return <Navigate to="/connexion" replace state={{ from: location.pathname + location.search }} />
  return <>{children}</>
}
