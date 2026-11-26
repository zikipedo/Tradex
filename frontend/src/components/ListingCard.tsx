import { Link } from 'react-router-dom'
import type { ListingSummary } from '../api/types'
import { formatRelative } from '../lib/format'
import { directionLabel, typeLabel } from '../lib/labels'
import { motifFor } from '../lib/motifs'
import { Motif, MotifFabric } from './Motif'
import './ListingCard.css'

export type CardVariant = 'feature' | 'standard' | 'row'

/**
 * Carte d'annonce. Trois variantes pour composer des listes rythmées plutôt qu'une grille uniforme :
 * - `feature` : grande image, titre en serif large ;
 * - `standard` : vignette verticale ;
 * - `row` : ligne compacte, sans image (listes denses).
 * Sans photo, la carte affiche le motif de sa catégorie sur fond kaolin (information, pas décoration).
 */
export function ListingCard({ listing, variant = 'standard' }: { listing: ListingSummary; variant?: CardVariant }) {
  const place = [listing.neighborhood, listing.city].filter(Boolean).join(', ')
  const isRequest = listing.direction === 'Request'
  const motif = motifFor(listing.categoryName)

  return (
    <article className={`lcard lcard--${variant}`}>
      {variant !== 'row' && (
        <div className={`lcard__media squircle${listing.coverPhotoUrl ? '' : ' is-fabric'}`}>
          {listing.coverPhotoUrl ? (
            <img src={listing.coverPhotoUrl} alt="" loading="lazy" decoding="async" />
          ) : (
            // Sans photo : un échantillon de tissu au motif de la catégorie (clair = offre, sombre = demande).
            <div className={`lcard__placeholder${isRequest ? ' is-request' : ''}`}>
              <MotifFabric name={motif} cell={variant === 'feature' ? 46 : 34} />
              <span className="lcard__seal">
                <Motif name={motif} size={variant === 'feature' ? 40 : 28} />
              </span>
            </div>
          )}
          <span className={`lcard__tag pill pill--plain${isRequest ? ' pill--ink' : ''}`}>{directionLabel[listing.direction]}</span>
          {listing.isDonation && <span className="lcard__gift pill pill--credit">Don</span>}
        </div>
      )}
      <div className="lcard__body">
        <p className="lcard__meta label">
          <Motif name={motif} size={14} />
          {listing.categoryName} · {typeLabel[listing.type]}
          {variant === 'row' && <> · {directionLabel[listing.direction]}</>}
        </p>
        <h3 className="lcard__title">
          <Link to={`/annonces/${listing.id}`} className="lcard__link">
            {listing.title}
          </Link>
        </h3>
        <p className="lcard__foot">
          <span>{place}</span>
          <span aria-hidden="true">·</span>
          <span>{listing.owner.displayName}</span>
          {listing.publishedAt && (
            <>
              <span aria-hidden="true">·</span>
              <time dateTime={listing.publishedAt}>{formatRelative(listing.publishedAt)}</time>
            </>
          )}
        </p>
      </div>
    </article>
  )
}
