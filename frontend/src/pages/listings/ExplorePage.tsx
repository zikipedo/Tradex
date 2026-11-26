import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { listingsApi } from '../../api/endpoints'
import type { ListingDirection, ListingSummary, ListingType } from '../../api/types'
import { ListingCard, type CardVariant } from '../../components/ListingCard'
import { ErrorNote, Loader } from '../../components/Loader'
import { Motif } from '../../components/Motif'
import { plural } from '../../lib/format'
import { errorMessage, useResource } from '../../lib/useAsync'
import { useReveal } from '../../motion/useReveal'
import './ExplorePage.css'

const PAGE_SIZE = 18

/**
 * Rythme de la grille : une grande carte toutes les sept annonces, le reste en vignettes.
 * Évite le mur de cartes identiques sans casser l'ordre chronologique.
 */
function variantFor(index: number): CardVariant {
  return index % 7 === 0 ? 'feature' : 'standard'
}

/** Recherche d'annonces : filtres synchronisés avec l'adresse de la page (partageable, retour arrière). */
export default function ExplorePage() {
  const [params, setParams] = useSearchParams()
  const root = useRef<HTMLDivElement>(null)
  const [query, setQuery] = useState(params.get('q') ?? '')
  const [items, setItems] = useState<ListingSummary[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  const categories = useResource(() => listingsApi.categories(), [])

  const filters = useMemo(
    () => ({
      q: params.get('q') ?? undefined,
      categoryId: params.get('categorie') ?? undefined,
      direction: (params.get('sens') as ListingDirection | null) ?? undefined,
      type: (params.get('type') as ListingType | null) ?? undefined,
      neighborhood: params.get('quartier') ?? undefined,
      donationOnly: params.get('don') === '1' ? true : undefined,
    }),
    [params],
  )

  // Recherche au fil de la frappe, avec un délai pour ne pas solliciter l'API à chaque lettre.
  useEffect(() => {
    const handle = setTimeout(() => update('q', query.trim() || null), 350)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query])

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    listingsApi
      .search({ ...filters, page, pageSize: PAGE_SIZE }, controller.signal)
      .then((result) => {
        setItems((previous) => (page === 1 ? result.items : [...previous, ...result.items]))
        setTotal(result.totalCount)
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) setError(errorMessage(err))
      })
      .finally(() => !controller.signal.aborted && setLoading(false))
    return () => controller.abort()
  }, [filters, page, attempt])

  useReveal(root, [items.length])

  function update(key: string, value: string | null) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (value) next.set(key, value)
        else next.delete(key)
        return next
      },
      { replace: true },
    )
    setPage(1)
  }

  const toggle = (key: string, value: string) => update(key, params.get(key) === value ? null : value)
  const activeCount = ['categorie', 'sens', 'type', 'quartier', 'don'].filter((k) => params.get(k)).length

  return (
    <div ref={root} className="explore wrap page">
      <header className="page-head explore__head">
        <p className="label">Toutes les annonces · Bamako</p>
        <h1 className="h1">
          Cherche, <em>propose, échange.</em>
        </h1>
        <div className="explore__search">
          <label htmlFor="recherche" className="sr-only">
            Rechercher une annonce
          </label>
          <Motif name="spirale" size={22} />
          <input
            id="recherche"
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Une perceuse, des cours d’anglais, un vélo…"
            autoComplete="off"
            enterKeyHint="search"
          />
        </div>
      </header>

      <div className="explore__layout">
        <aside className="explore__filters" aria-label="Filtres">
          <div className="filter-group" role="group" aria-label="Sens de l’annonce">
            <p className="label">Sens</p>
            <button type="button" className="chip" aria-pressed={filters.direction === 'Offer'} onClick={() => toggle('sens', 'Offer')}>
              On propose
            </button>
            <button type="button" className="chip" aria-pressed={filters.direction === 'Request'} onClick={() => toggle('sens', 'Request')}>
              On cherche
            </button>
          </div>
          <div className="filter-group" role="group" aria-label="Nature">
            <p className="label">Nature</p>
            <button type="button" className="chip" aria-pressed={filters.type === 'Good'} onClick={() => toggle('type', 'Good')}>
              Objets
            </button>
            <button type="button" className="chip" aria-pressed={filters.type === 'Service'} onClick={() => toggle('type', 'Service')}>
              Services
            </button>
            <button type="button" className="chip" aria-pressed={filters.donationOnly === true} onClick={() => toggle('don', '1')}>
              Dons seulement
            </button>
          </div>
          <div className="filter-group">
            <label className="label" htmlFor="categorie">
              Catégorie
            </label>
            <select id="categorie" className="filter-select" value={filters.categoryId ?? ''} onChange={(e) => update('categorie', e.target.value || null)}>
              <option value="">Toutes</option>
              {(categories.data ?? [])
                .filter((c) => c.isActive)
                .map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
            </select>
          </div>
          <div className="filter-group">
            <label className="label" htmlFor="quartier">
              Quartier
            </label>
            <input
              id="quartier"
              className="filter-select"
              defaultValue={filters.neighborhood ?? ''}
              placeholder="Hamdallaye, ACI 2000…"
              onBlur={(e) => update('quartier', e.target.value.trim() || null)}
              onKeyDown={(e) => e.key === 'Enter' && update('quartier', e.currentTarget.value.trim() || null)}
            />
          </div>
          {activeCount > 0 && (
            <button
              type="button"
              className="btn btn--ghost btn--small"
              onClick={() => {
                setParams(query ? { q: query } : {}, { replace: true })
                setPage(1)
              }}
            >
              Effacer les filtres ({activeCount})
            </button>
          )}
        </aside>

        <section className="explore__results" aria-live="polite" aria-busy={loading}>
          <p className="explore__count muted">{loading && page === 1 ? 'Recherche…' : plural(total, 'annonce trouvée', 'annonces trouvées')}</p>

          {error && <ErrorNote message={error} onRetry={() => setAttempt((n) => n + 1)} />}

          {!loading && !error && items.length === 0 && (
            <div className="empty">
              <Motif name="spirale" size={40} />
              <h2 className="h3">Rien pour cette recherche, pour l’instant.</h2>
              <p className="muted">Essaie un mot plus simple, retire un filtre… ou publie toi-même ce que tu cherches : quelqu’un le verra.</p>
              <Link to="/publier" className="btn btn--action">
                Publier « je cherche »
              </Link>
            </div>
          )}

          <div className="explore__grid">
            {items.map((listing, index) => (
              <div key={listing.id} className={`explore__cell explore__cell--${variantFor(index)}`} data-reveal>
                <ListingCard listing={listing} variant={variantFor(index)} />
              </div>
            ))}
          </div>

          {loading && page > 1 && <Loader label="Chargement de la suite" />}
          {!loading && items.length < total && (
            <div className="pager">
              <button type="button" className="btn" onClick={() => setPage((p) => p + 1)}>
                Voir plus d’annonces
              </button>
            </div>
          )}
        </section>
      </div>
    </div>
  )
}
