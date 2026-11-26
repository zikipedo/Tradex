import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { authApi } from '../../api/endpoints'
import { useAuth } from '../../auth/AuthContext'
import { Loader } from '../../components/Loader'
import { errorMessage } from '../../lib/useAsync'
import { AuthShell } from './AuthShell'

/** Ouvre le lien reçu par e-mail (`/confirmer-email?token=…`) et confirme l'adresse. */
export default function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const token = params.get('token')
  const { reloadMe } = useAuth()
  const [state, setState] = useState<'pending' | 'ok' | 'error'>(token ? 'pending' : 'error')
  const [message, setMessage] = useState(token ? '' : 'Ce lien est incomplet. Ouvre-le directement depuis l’e-mail reçu.')
  const sent = useRef(false)

  useEffect(() => {
    // Le jeton est à usage unique : on ne l'envoie qu'une fois, même en double rendu de développement.
    if (!token || sent.current) return
    sent.current = true
    authApi
      .confirmEmail(token)
      .then(() => {
        setState('ok')
        void reloadMe()
      })
      .catch((err: unknown) => {
        setState('error')
        setMessage(errorMessage(err))
      })
  }, [token, reloadMe])

  return (
    <AuthShell
      motif="croix"
      quote={
        <>
          Un e-mail confirmé, <em>c’est une confiance de plus.</em>
        </>
      }
    >
      <div className="auth__form" role="status" aria-live="polite">
        {state === 'pending' && <Loader label="Confirmation de ton adresse" />}
        {state === 'ok' && (
          <>
            <h1 className="h2">Adresse confirmée.</h1>
            <p className="muted">Tu peux maintenant publier des annonces et proposer des échanges.</p>
            <Link to="/annonces" className="btn btn--action">
              Voir les annonces
            </Link>
          </>
        )}
        {state === 'error' && (
          <>
            <h1 className="h2">Ce lien ne fonctionne pas.</h1>
            <p className="alert">{message}</p>
            <p className="muted">Il a peut-être expiré (24 heures) ou déjà servi. Tu peux en demander un nouveau depuis ton profil.</p>
            <Link to="/moi" className="btn">
              Aller à mon profil
            </Link>
          </>
        )}
      </div>
    </AuthShell>
  )
}
