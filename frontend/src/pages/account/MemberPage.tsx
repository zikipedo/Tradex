import { useParams } from 'react-router-dom'
import { usersApi } from '../../api/endpoints'
import { ErrorNote, Loader } from '../../components/Loader'
import { formatDate, formatRelative, initials, plural } from '../../lib/format'
import { useResource } from '../../lib/useAsync'

/** Profil public d'un membre : aucune donnée de contact, seulement ce qui aide à faire confiance. */
export default function MemberPage() {
  const { id = '' } = useParams()
  const profile = useResource(() => usersApi.profile(id), [id])
  const reviews = useResource(() => usersApi.reviews(id), [id])

  if (profile.loading && !profile.data) return <Loader label="Chargement du profil" />
  if (profile.error || !profile.data)
    return (
      <div className="wrap page">
        <ErrorNote message={profile.error ?? 'Membre introuvable.'} onRetry={profile.reload} />
      </div>
    )

  const p = profile.data
  return (
    <div className="wrap page">
      <header className="page-head" style={{ gridTemplateColumns: 'auto 1fr', alignItems: 'center', columnGap: 'var(--s5)' }}>
        <span className="avatar" style={{ width: 88, height: 88, fontSize: '1.6rem' }} aria-hidden="true">
          {initials(p.displayName)}
        </span>
        <div>
          <p className="label">{[p.neighborhood, p.city].filter(Boolean).join(', ')} · membre depuis le {formatDate(p.memberSince)}</p>
          <h1 className="h1">{p.displayName}</h1>
        </div>
      </header>

      <dl className="trust-mini">
        <div>
          <dt>Échanges terminés</dt>
          <dd className="tabular">{p.completedExchanges}</dd>
        </div>
        <div>
          <dt>Note moyenne</dt>
          <dd className="tabular">{p.averageRating !== null ? `${p.averageRating.toFixed(1)} / 5` : '—'}</dd>
        </div>
        <div>
          <dt>E-mail</dt>
          <dd>{p.emailConfirmed ? 'Vérifié' : 'Non vérifié'}</dd>
        </div>
      </dl>

      {p.bio && <p className="lede" style={{ marginTop: 'var(--s6)', maxWidth: '60ch' }}>{p.bio}</p>}

      <section style={{ marginTop: 'var(--s7)' }} aria-labelledby="reviews-title">
        <h2 id="reviews-title" className="h2" style={{ marginBottom: 'var(--s5)' }}>
          {plural(p.reviewCount, 'avis', 'avis')}
        </h2>
        {reviews.data?.items.length ? (
          <ul className="rows">
            {reviews.data.items.map((r) => (
              <li key={r.id} className="row" style={{ gridTemplateColumns: 'auto 1fr' }}>
                <span className="pill pill--plain tabular" role="img" aria-label={`${r.rating} sur 5`}>
                  {'●'.repeat(r.rating)}
                  <span style={{ opacity: 0.25 }}>{'●'.repeat(5 - r.rating)}</span>
                </span>
                <div>
                  {r.comment && <p>{r.comment}</p>}
                  <p className="row__meta">
                    {r.authorDisplayName} · {formatRelative(r.createdAt)}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        ) : (
          <p className="muted">Pas encore d’avis. Les avis n’arrivent qu’après un échange réellement terminé.</p>
        )}
      </section>
    </div>
  )
}
