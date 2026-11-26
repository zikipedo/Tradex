import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { authApi, usersApi } from '../../api/endpoints'
import { useAuth } from '../../auth/AuthContext'
import { TextArea, TextField } from '../../components/Field'
import { formatDate } from '../../lib/format'
import { useAction } from '../../lib/useAsync'

/** Mon profil : informations publiques modifiables, confirmation d'e-mail, déconnexion. */
export default function ProfilePage() {
  const { me, reloadMe, logout } = useAuth()
  const navigate = useNavigate()
  // La page est protégée par RequireAuth : le profil est déjà chargé au premier rendu.
  const [form, setForm] = useState(() => ({
    displayName: me?.displayName ?? '',
    city: me?.city ?? '',
    neighborhood: me?.neighborhood ?? '',
    bio: me?.bio ?? '',
  }))
  const [saved, setSaved] = useState(false)
  const [resent, setResent] = useState<string | null>(null)
  const save = useAction(usersApi.updateMe)
  const resend = useAction(authApi.resendConfirmation)

  if (!me) return null

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setSaved(false)
    const result = await save.run({
      displayName: form.displayName.trim(),
      city: form.city.trim(),
      neighborhood: form.neighborhood.trim() || null,
      bio: form.bio.trim() || null,
    })
    if (result) {
      await reloadMe()
      setSaved(true)
    }
  }

  return (
    <div className="wrap page">
      <header className="page-head page-head--split">
        <div>
          <p className="label">Mon profil · membre depuis le {formatDate(me.createdAt)}</p>
          <h1 className="h1">{me.displayName}</h1>
        </div>
        <Link to={`/membres/${me.id}`} className="btn">
          Voir mon profil public
        </Link>
      </header>

      <div className="form-grid" style={{ alignItems: 'start', gap: 'var(--s7)' }}>
        <form className="panel" onSubmit={onSubmit} noValidate>
          <h2 className="h3">Ce que les autres voient</h2>
          <TextField label="Nom affiché" value={form.displayName} maxLength={60} onChange={(e) => setForm({ ...form, displayName: e.target.value })} error={save.fieldErrors.displayname} />
          <div className="form-grid">
            <TextField label="Ville" value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} error={save.fieldErrors.city} />
            <TextField label="Quartier" value={form.neighborhood} onChange={(e) => setForm({ ...form, neighborhood: e.target.value })} error={save.fieldErrors.neighborhood} />
          </div>
          <TextArea
            label="Quelques mots sur toi"
            value={form.bio}
            maxLength={500}
            rows={4}
            onChange={(e) => setForm({ ...form, bio: e.target.value })}
            placeholder="Couturière à Lafiabougou, j’aime réparer plutôt que jeter."
            hint={`${form.bio.length} / 500 · ne mets pas ton numéro de téléphone ici, utilise la conversation d’un échange.`}
            error={save.fieldErrors.bio}
          />
          {save.error && <p className="alert">{save.error}</p>}
          {saved && (
            <p className="alert alert--ok" role="status">
              Profil enregistré.
            </p>
          )}
          <button type="submit" className="btn btn--action" disabled={save.pending}>
            Enregistrer
          </button>
        </form>

        <div style={{ display: 'grid', gap: 'var(--s5)' }}>
          <section className="panel panel--kaolin">
            <h2 className="h3">Adresse e-mail</h2>
            <p>{me.email}</p>
            {me.emailConfirmed ? (
              <p className="pill pill--credit">Confirmée</p>
            ) : (
              <>
                <p className="muted">Pas encore confirmée : tu ne peux ni publier ni échanger.</p>
                {resent ? (
                  <p className="alert alert--ok">{resent}</p>
                ) : (
                  <button
                    type="button"
                    className="btn"
                    disabled={resend.pending}
                    onClick={async () => {
                      const result = await resend.run(me.email)
                      if (result) setResent(result.message)
                    }}
                  >
                    Renvoyer le lien
                  </button>
                )}
                {resend.error && <p className="alert">{resend.error}</p>}
              </>
            )}
          </section>
          <section className="panel">
            <h2 className="h3">Session</h2>
            <p className="muted">Se déconnecter ferme la session sur cet appareil.</p>
            <button
              type="button"
              className="btn"
              onClick={async () => {
                await logout()
                navigate('/')
              }}
            >
              Se déconnecter
            </button>
          </section>
        </div>
      </div>
    </div>
  )
}
