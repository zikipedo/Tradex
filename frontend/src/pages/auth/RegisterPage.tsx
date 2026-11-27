import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { authApi } from '../../api/endpoints'
import { DevMailNotice } from '../../components/DevMailNotice'
import { TextField } from '../../components/Field'
import { useAction } from '../../lib/useAsync'
import { AuthShell } from './AuthShell'

const MIN_PASSWORD = 10

/**
 * Inscription. L'API répond toujours la même chose, que l'e-mail soit déjà pris ou non
 * (protection contre l'énumération des comptes) : on affiche donc un message neutre.
 */
export default function RegisterPage() {
  const [form, setForm] = useState({ displayName: '', email: '', password: '', city: 'Bamako', neighborhood: '' })
  const [done, setDone] = useState<string | null>(null)
  const { run, pending, error, fieldErrors } = useAction(authApi.register)
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })
  const passwordTooShort = form.password.length > 0 && form.password.length < MIN_PASSWORD

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const result = await run({
      displayName: form.displayName.trim(),
      email: form.email.trim(),
      password: form.password,
      city: form.city.trim(),
      neighborhood: form.neighborhood.trim() || null,
    })
    if (result) setDone(result.message)
  }

  return (
    <AuthShell
      motif="losange"
      quote={
        <>
          Tout le monde commence <em>à zéro.</em>
        </>
      }
    >
      {done ? (
        <div className="auth__form" role="status">
          <p className="label muted">Presque fini</p>
          <h1 className="h2">Regarde ta boîte mail.</h1>
          <p className="muted">{done}</p>
          <DevMailNotice />
          <p className="muted">Le lien de confirmation est valable 24 heures. Sans confirmation, tu peux regarder les annonces mais pas encore échanger.</p>
          <Link to="/connexion" className="btn btn--action">
            Aller à la connexion
          </Link>
        </div>
      ) : (
        <form className="auth__form" onSubmit={onSubmit} noValidate>
          <div>
            <p className="label muted">Créer un compte</p>
            <h1 className="h2">Rejoins les échanges.</h1>
          </div>
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
          <TextField
            label="Ton nom affiché"
            autoComplete="nickname"
            required
            maxLength={60}
            value={form.displayName}
            onChange={set('displayName')}
            placeholder="Awa T."
            hint="Visible par les autres membres. Ton e-mail, lui, reste privé."
            error={fieldErrors.displayname}
          />
          <TextField
            label="Adresse e-mail"
            type="email"
            autoComplete="email"
            inputMode="email"
            required
            value={form.email}
            onChange={set('email')}
            error={fieldErrors.email}
          />
          <TextField
            label="Mot de passe"
            type="password"
            autoComplete="new-password"
            required
            minLength={MIN_PASSWORD}
            value={form.password}
            onChange={set('password')}
            hint={`Au moins ${MIN_PASSWORD} caractères. Une phrase courte est plus sûre qu’un mot compliqué.`}
            error={passwordTooShort ? `Encore ${MIN_PASSWORD - form.password.length} caractère(s).` : fieldErrors.password}
          />
          <div className="form-grid">
            <TextField label="Ville" required value={form.city} onChange={set('city')} error={fieldErrors.city} />
            <TextField
              label="Quartier"
              value={form.neighborhood}
              onChange={set('neighborhood')}
              placeholder="Badalabougou"
              error={fieldErrors.neighborhood}
            />
          </div>
          <button
            className="btn btn--action"
            type="submit"
            disabled={pending || !form.displayName || !form.email || form.password.length < MIN_PASSWORD || !form.city}
          >
            {pending ? 'Création…' : 'Créer mon compte'}
          </button>
          <p className="auth__alt">
            Déjà membre ? <Link to="/connexion">Se connecter</Link>
          </p>
        </form>
      )}
    </AuthShell>
  )
}
