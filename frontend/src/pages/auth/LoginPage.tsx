import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { TextField } from '../../components/Field'
import { safeRedirect } from '../../lib/safeRedirect'
import { useAction } from '../../lib/useAsync'
import { AuthShell } from './AuthShell'

/** Connexion par e-mail et mot de passe. Le message d'erreur ne dit jamais si l'e-mail existe. */
export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = safeRedirect((location.state as { from?: unknown } | null)?.from)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const { run, pending, error } = useAction(login)

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (await run(email.trim(), password)) navigate(from, { replace: true })
  }

  return (
    <AuthShell
      quote={
        <>
          Bon retour. <em>Le fil t’attendait.</em>
        </>
      }
    >
      <form className="auth__form" onSubmit={onSubmit} noValidate>
        <div>
          <p className="label muted">Se connecter</p>
          <h1 className="h2">Content de te revoir.</h1>
        </div>
        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        <TextField
          label="Adresse e-mail"
          type="email"
          autoComplete="email"
          inputMode="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="awa.traore@exemple.ml"
        />
        <TextField
          label="Mot de passe"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <div className="form-actions">
          <button className="btn btn--action" type="submit" disabled={pending || !email || !password}>
            {pending ? 'Connexion…' : 'Se connecter'}
          </button>
          <Link to="/mot-de-passe-oublie" className="auth__alt">
            Mot de passe oublié ?
          </Link>
        </div>
        <p className="auth__alt">
          Pas encore de compte ? <Link to="/inscription">Créer un compte</Link>
        </p>
      </form>
    </AuthShell>
  )
}
