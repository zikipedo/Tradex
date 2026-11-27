import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { authApi } from '../../api/endpoints'
import { DevMailNotice } from '../../components/DevMailNotice'
import { TextField } from '../../components/Field'
import { useAction } from '../../lib/useAsync'
import { AuthShell } from './AuthShell'

/** Demande d'un lien de réinitialisation. Réponse identique que l'adresse existe ou non. */
export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [done, setDone] = useState<string | null>(null)
  const { run, pending, error } = useAction(authApi.forgotPassword)

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const result = await run(email.trim())
    if (result) setDone(result.message)
  }

  return (
    <AuthShell
      motif="spirale"
      quote={
        <>
          On retrouve <em>le bout du fil.</em>
        </>
      }
    >
      {done ? (
        <div className="auth__form" role="status">
          <h1 className="h2">C’est envoyé.</h1>
          <p className="muted">{done}</p>
          <DevMailNotice />
          <p className="muted">Le lien est valable une heure.</p>
          <Link to="/connexion" className="btn">
            Retour à la connexion
          </Link>
        </div>
      ) : (
        <form className="auth__form" onSubmit={onSubmit} noValidate>
          <div>
            <p className="label muted">Mot de passe oublié</p>
            <h1 className="h2">Pas de souci.</h1>
            <p className="muted">Indique ton adresse : si un compte existe, tu recevras un lien pour choisir un nouveau mot de passe.</p>
          </div>
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
          <TextField label="Adresse e-mail" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          <button className="btn btn--action" type="submit" disabled={pending || !email}>
            {pending ? 'Envoi…' : 'Recevoir le lien'}
          </button>
        </form>
      )}
    </AuthShell>
  )
}
