import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { authApi } from '../../api/endpoints'
import { TextField } from '../../components/Field'
import { useAction } from '../../lib/useAsync'
import { AuthShell } from './AuthShell'

const MIN_PASSWORD = 10

/** Choix d'un nouveau mot de passe depuis le lien reçu par e-mail. Toutes les sessions sont fermées par l'API. */
export default function ResetPasswordPage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [done, setDone] = useState(false)
  const { run, pending, error, fieldErrors } = useAction(authApi.resetPassword)
  const mismatch = confirm.length > 0 && confirm !== password

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (mismatch) return
    await run(token, password)
    setDone(true)
  }

  return (
    <AuthShell
      motif="peigne"
      quote={
        <>
          Un nouveau départ, <em>même compte.</em>
        </>
      }
    >
      {done && !error ? (
        <div className="auth__form" role="status">
          <h1 className="h2">Mot de passe changé.</h1>
          <p className="muted">Par sécurité, tes autres sessions ont été fermées.</p>
          <Link to="/connexion" className="btn btn--action">
            Se connecter
          </Link>
        </div>
      ) : (
        <form className="auth__form" onSubmit={onSubmit} noValidate>
          <h1 className="h2">Nouveau mot de passe</h1>
          {!token && <p className="alert">Ce lien est incomplet. Ouvre-le directement depuis l’e-mail reçu.</p>}
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
          <TextField
            label="Nouveau mot de passe"
            type="password"
            autoComplete="new-password"
            minLength={MIN_PASSWORD}
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            hint={`Au moins ${MIN_PASSWORD} caractères.`}
            error={fieldErrors.newpassword}
          />
          <TextField
            label="Confirme-le"
            type="password"
            autoComplete="new-password"
            required
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            error={mismatch ? 'Les deux mots de passe ne sont pas identiques.' : undefined}
          />
          <button className="btn btn--action" type="submit" disabled={pending || !token || password.length < MIN_PASSWORD || confirm !== password}>
            {pending ? 'Enregistrement…' : 'Enregistrer'}
          </button>
        </form>
      )}
    </AuthShell>
  )
}
