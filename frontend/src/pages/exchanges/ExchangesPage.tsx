import { useState } from 'react'
import { Link } from 'react-router-dom'
import { exchangesApi } from '../../api/endpoints'
import type { ExchangeRoleFilter } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { ErrorNote, Loader } from '../../components/Loader'
import { Motif } from '../../components/Motif'
import { counterpart } from '../../lib/exchangeActions'
import { formatMinutes, formatRelative } from '../../lib/format'
import { exchangeStatusLabel, exchangeStatusTone, kindLabel } from '../../lib/labels'
import { useResource } from '../../lib/useAsync'

const TABS: { key: ExchangeRoleFilter; label: string }[] = [
  { key: 'All', label: 'Tous' },
  { key: 'Received', label: 'Reçus' },
  { key: 'Sent', label: 'Envoyés' },
]

const KIND_MOTIF = { Barter: 'losange', TimeBank: 'echelle', Donation: 'soleil' } as const

/** Mes échanges : propositions reçues et envoyées, avec leur état. */
export default function ExchangesPage() {
  const { me } = useAuth()
  const [role, setRole] = useState<ExchangeRoleFilter>('All')
  const [page, setPage] = useState(1)
  const { data, error, loading, reload } = useResource(() => exchangesApi.list(role, undefined, page), [role, page])

  return (
    <div className="wrap page">
      <header className="page-head">
        <p className="label">Mes échanges</p>
        <h1 className="h1">
          Chaque fil, <em>et où il en est.</em>
        </h1>
      </header>

      <div className="tabs" role="tablist" aria-label="Filtrer les échanges">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            role="tab"
            aria-selected={role === t.key}
            onClick={() => {
              setRole(t.key)
              setPage(1)
            }}
          >
            {t.label}
          </button>
        ))}
      </div>

      {loading && <Loader label="Chargement de tes échanges" />}
      {error && <ErrorNote message={error} onRetry={reload} />}
      {!loading && !error && data?.items.length === 0 && (
        <div className="empty">
          <Motif name="losange" size={40} />
          <h2 className="h3">Aucun échange pour l’instant.</h2>
          <p className="muted">Trouve une annonce qui te plaît et propose un troc, des heures, ou demande un don.</p>
          <Link to="/annonces" className="btn btn--action">
            Voir les annonces
          </Link>
        </div>
      )}

      {data && data.items.length > 0 && me && (
        <ul className="rows">
          {data.items.map((e) => {
            const other = counterpart(e, me.id)
            const sent = e.requester.id === me.id
            return (
              <li key={e.id} className="row">
                <span className="row__icon" aria-hidden="true">
                  <Motif name={KIND_MOTIF[e.kind]} size={22} />
                </span>
                <div>
                  <p className="row__title">
                    <Link to={`/echanges/${e.id}`}>{e.targetListing.title}</Link>
                  </p>
                  <p className="row__meta">
                    {kindLabel[e.kind]}
                    {e.durationMinutes && <> · {formatMinutes(e.durationMinutes)}</>}
                    {e.offeredListing && <> · contre « {e.offeredListing.title} »</>} · {sent ? 'envoyé à' : 'reçu de'} {other.displayName} ·{' '}
                    {formatRelative(e.createdAt)}
                  </p>
                </div>
                <span className={`pill pill--${exchangeStatusTone[e.status]}`}>{exchangeStatusLabel[e.status]}</span>
              </li>
            )
          })}
        </ul>
      )}

      {data && data.totalPages > 1 && (
        <div className="pager form-actions">
          <button type="button" className="btn btn--small" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Précédent
          </button>
          <span className="muted">
            Page {page} sur {data.totalPages}
          </span>
          <button type="button" className="btn btn--small" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>
            Suivant
          </button>
        </div>
      )}
    </div>
  )
}
