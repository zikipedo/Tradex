import { useState } from 'react'
import { Link } from 'react-router-dom'
import { listingsApi } from '../../api/endpoints'
import type { ListingStatus, ListingSummary } from '../../api/types'
import { ErrorNote, Loader } from '../../components/Loader'
import { Motif } from '../../components/Motif'
import { motifFor } from '../../lib/motifs'
import { formatRelative, plural } from '../../lib/format'
import { directionLabel, listingStatusLabel } from '../../lib/labels'
import { useResource } from '../../lib/useAsync'

const FILTERS: { key: 'all' | ListingStatus; label: string }[] = [
  { key: 'all', label: 'Toutes' },
  { key: 'Published', label: 'Publiées' },
  { key: 'Draft', label: 'Brouillons' },
  { key: 'Reserved', label: 'Réservées' },
  { key: 'Exchanged', label: 'Échangées' },
  { key: 'Archived', label: 'Archivées' },
]

const statusTone: Record<ListingStatus, string> = {
  Draft: '',
  Published: 'pill--credit',
  Reserved: 'pill--ink',
  Exchanged: 'pill--credit',
  Archived: '',
  Suspended: 'pill--debit',
}

/** Mes annonces, tous statuts, avec un filtre par statut. */
export default function MyListingsPage() {
  const [page, setPage] = useState(1)
  const [filter, setFilter] = useState<'all' | ListingStatus>('all')
  const { data, error, loading, reload } = useResource(() => listingsApi.mine(page), [page])
  const items: ListingSummary[] = (data?.items ?? []).filter((l) => filter === 'all' || l.status === filter)

  return (
    <div className="wrap page">
      <header className="page-head page-head--split">
        <div>
          <p className="label">Mes annonces</p>
          <h1 className="h1">
            Ce que tu <em>mets en circulation.</em>
          </h1>
        </div>
        <Link to="/publier" className="btn btn--action">
          Nouvelle annonce
        </Link>
      </header>

      <div className="tabs" role="tablist" aria-label="Filtrer par statut">
        {FILTERS.map((f) => (
          <button key={f.key} type="button" role="tab" aria-selected={filter === f.key} onClick={() => setFilter(f.key)}>
            {f.label}
          </button>
        ))}
      </div>

      {loading && <Loader label="Chargement de tes annonces" />}
      {error && <ErrorNote message={error} onRetry={reload} />}
      {!loading && !error && items.length === 0 && (
        <div className="empty">
          <Motif name="spirale" size={40} />
          <h2 className="h3">{filter === 'all' ? 'Tu n’as encore rien publié.' : 'Aucune annonce dans cette catégorie.'}</h2>
          <p className="muted">Un objet qui dort, un savoir-faire, un coup de main : tout peut s’échanger.</p>
          <Link to="/publier" className="btn btn--action">
            Publier une annonce
          </Link>
        </div>
      )}

      {items.length > 0 && (
        <ul className="rows">
          {items.map((l) => (
            <li key={l.id} className="row">
              <span className="row__icon" aria-hidden="true">
                <Motif name={motifFor(l.categoryName)} size={22} />
              </span>
              <div>
                <p className="row__title">
                  <Link to={l.status === 'Draft' ? `/mes-annonces/${l.id}/modifier` : `/annonces/${l.id}`}>{l.title}</Link>
                </p>
                <p className="row__meta">
                  {directionLabel[l.direction]} · {l.categoryName}
                  {l.publishedAt && <> · publiée {formatRelative(l.publishedAt)}</>}
                </p>
              </div>
              <span className={`pill ${statusTone[l.status]}`}>{listingStatusLabel[l.status]}</span>
            </li>
          ))}
        </ul>
      )}

      {data && data.totalPages > 1 && (
        <div className="pager form-actions">
          <button type="button" className="btn btn--small" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Précédent
          </button>
          <span className="muted">
            Page {page} sur {data.totalPages} · {plural(data.totalCount, 'annonce')}
          </span>
          <button type="button" className="btn btn--small" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>
            Suivant
          </button>
        </div>
      )}
    </div>
  )
}
