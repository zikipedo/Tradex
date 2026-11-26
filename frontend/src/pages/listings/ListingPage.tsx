import { useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { listingsApi } from '../../api/endpoints'
import type { Listing, ReportReason } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { SelectField, TextArea } from '../../components/Field'
import { ErrorNote, Loader } from '../../components/Loader'
import { Motif, MotifFabric } from '../../components/Motif'
import { motifFor } from '../../lib/motifs'
import { ProtectedImage } from '../../components/ProtectedImage'
import { formatDate, initials } from '../../lib/format'
import { conditionLabel, directionLabel, listingStatusLabel, reportReasonLabel, typeLabel } from '../../lib/labels'
import { useAction, useResource } from '../../lib/useAsync'
import { useReveal } from '../../motion/useReveal'
import { ProposePanel } from './ProposePanel'
import './ListingPage.css'

/** Détail d'une annonce : photos, description, auteur, et selon le lecteur, proposer un échange ou gérer l'annonce. */
export default function ListingPage() {
  const { id = '' } = useParams()
  const { me } = useAuth()
  const root = useRef<HTMLDivElement>(null)
  const { data: listing, setData, error, loading, reload } = useResource(() => listingsApi.get(id), [id])
  useReveal(root, [listing?.id])

  if (loading && !listing) return <Loader label="Chargement de l’annonce" />
  if (error || !listing)
    return (
      <div className="wrap page">
        <ErrorNote message={error ?? 'Annonce introuvable.'} onRetry={reload} />
      </div>
    )

  const isOwner = me?.id === listing.owner.id
  const place = [listing.neighborhood, listing.city].filter(Boolean).join(', ')

  return (
    <div ref={root} className="listing wrap page">
      <Link to="/annonces" className="back-link">
        <span aria-hidden="true">←</span> Toutes les annonces
      </Link>

      <div className="listing__grid">
        <header className="listing__head">
          <p className="listing__tags" data-reveal>
            <span className={`pill pill--plain${listing.direction === 'Request' ? ' pill--ink' : ''}`}>{directionLabel[listing.direction]}</span>
            <span className="pill pill--plain">{typeLabel[listing.type]}</span>
            {listing.isDonation && <span className="pill pill--credit">Don</span>}
            {listing.status !== 'Published' && <span className="pill pill--debit">{listingStatusLabel[listing.status]}</span>}
          </p>
          <h1 className="h1 listing__title" data-reveal>
            {listing.title}
          </h1>
          <p className="listing__meta label" data-reveal>
            <Motif name={motifFor(listing.category.name)} size={14} /> {listing.category.name} · {place}
            {listing.publishedAt && <> · publiée le {formatDate(listing.publishedAt)}</>}
          </p>
        </header>

        <Gallery listing={listing} />

        <div className="listing__main">
          <div className="listing__body" data-reveal>
            <p className="listing__description">{listing.description}</p>
            <dl className="listing__facts">
              {listing.condition && (
                <div>
                  <dt>État</dt>
                  <dd>{conditionLabel[listing.condition]}</dd>
                </div>
              )}
              {listing.availability && (
                <div>
                  <dt>Disponibilités</dt>
                  <dd>{listing.availability}</dd>
                </div>
              )}
              {listing.wantedInReturn && !listing.isDonation && (
                <div>
                  <dt>Ce qui l’intéresse en retour</dt>
                  <dd>{listing.wantedInReturn}</dd>
                </div>
              )}
            </dl>
          </div>

          <Link to={`/membres/${listing.owner.id}`} className="owner squircle" data-reveal>
            <span className="avatar" aria-hidden="true">
              {initials(listing.owner.displayName)}
            </span>
            <span>
              <span className="label muted">Publiée par</span>
              <strong>{listing.owner.displayName}</strong>
            </span>
            <span className="owner__arrow" aria-hidden="true">
              →
            </span>
          </Link>
        </div>

        <aside className="listing__side" data-reveal>
          {isOwner ? (
            <OwnerActions listing={listing} onChange={setData} />
          ) : listing.status === 'Published' ? (
            <ProposePanel listing={listing} />
          ) : (
            <div className="panel panel--kaolin">
              <h2 className="h3">Plus disponible pour l’instant.</h2>
              <p className="muted">Cette annonce est {listingStatusLabel[listing.status].toLowerCase()}.</p>
            </div>
          )}
          {!isOwner && me && <ReportButton listingId={listing.id} />}
        </aside>
      </div>
    </div>
  )
}

/** Galerie : une grande image et des vignettes ; sans photo, le motif de la catégorie. */
function Gallery({ listing }: { listing: Listing }) {
  const [index, setIndex] = useState(0)
  const photo = listing.photos[index]
  return (
    <div className="gallery" data-reveal>
      <div className="gallery__main squircle">
        {photo ? (
          // Une annonce non publiée n'est visible que par son auteur : ses photos demandent le jeton de session.
          listing.status === 'Published' ? (
            <img src={photo.url} alt={`Photo ${index + 1} sur ${listing.photos.length} : ${listing.title}`} width={photo.width} height={photo.height} />
          ) : (
            <ProtectedImage src={photo.url} alt={`Photo ${index + 1} : ${listing.title}`} />
          )
        ) : (
          <div className={`gallery__empty${listing.direction === 'Request' ? ' is-request' : ''}`}>
            <MotifFabric name={motifFor(listing.category.name)} cell={58} />
            <span className="lcard__seal gallery__seal">
              <Motif name={motifFor(listing.category.name)} size={48} />
            </span>
            <span className="label gallery__caption">Pas encore de photo</span>
          </div>
        )}
      </div>
      {listing.photos.length > 1 && (
        <div className="gallery__thumbs" role="group" aria-label="Choisir une photo">
          {listing.photos.map((p, i) => (
            <button key={p.id} type="button" aria-label={`Photo ${i + 1}`} aria-pressed={i === index} onClick={() => setIndex(i)}>
              {listing.status === 'Published' ? <img src={p.url} alt="" loading="lazy" /> : <ProtectedImage src={p.url} alt="" />}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

/** Actions de l'auteur : modifier, publier un brouillon, archiver. */
function OwnerActions({ listing, onChange }: { listing: Listing; onChange: (l: Listing) => void }) {
  const navigate = useNavigate()
  const publish = useAction(listingsApi.publish)
  const archive = useAction(listingsApi.archive)
  const error = publish.error ?? archive.error

  return (
    <div className="panel">
      <p className="label muted">Ton annonce · {listingStatusLabel[listing.status]}</p>
      <h2 className="h3">Que veux-tu faire ?</h2>
      {error && <p className="alert">{error}</p>}
      <div className="form-actions">
        {listing.status === 'Draft' && (
          <button
            type="button"
            className="btn btn--action"
            disabled={publish.pending}
            onClick={async () => {
              const updated = await publish.run(listing.id)
              if (updated) onChange(updated)
            }}
          >
            Publier
          </button>
        )}
        {(listing.status === 'Draft' || listing.status === 'Published') && (
          <Link to={`/mes-annonces/${listing.id}/modifier`} className="btn">
            Modifier
          </Link>
        )}
        {listing.status === 'Published' && (
          <button
            type="button"
            className="btn btn--ghost"
            disabled={archive.pending}
            onClick={async () => {
              if (!window.confirm('Archiver cette annonce ? Elle ne sera plus visible dans les recherches.')) return
              const updated = await archive.run(listing.id)
              if (updated) navigate('/mes-annonces')
            }}
          >
            Archiver
          </button>
        )}
      </div>
    </div>
  )
}

const REASONS = Object.keys(reportReasonLabel) as ReportReason[]

/** Signalement d'une annonce, dans une boîte de dialogue native (gestion du focus et de la touche Échap). */
function ReportButton({ listingId }: { listingId: string }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const [reason, setReason] = useState<ReportReason>('NoLongerAvailable')
  const [details, setDetails] = useState('')
  const [receipt, setReceipt] = useState<string | null>(null)
  const { run, pending, error } = useAction(listingsApi.report)

  return (
    <>
      <button type="button" className="report-link" onClick={() => dialog.current?.showModal()}>
        Signaler cette annonce
      </button>
      <dialog ref={dialog} className="dialog" aria-labelledby="report-title">
        <form
          method="dialog"
          className="dialog__body"
          onSubmit={async (event) => {
            if (receipt) return
            event.preventDefault()
            const result = await run(listingId, reason, details.trim() || null)
            if (result) setReceipt(result.message)
          }}
        >
          <h2 id="report-title" className="h3">
            Signaler l’annonce
          </h2>
          {receipt ? (
            <p className="alert alert--ok" role="status">
              {receipt}
            </p>
          ) : (
            <>
              <p className="muted">Les modérateurs verront ton signalement. L’auteur ne sait pas qui l’a fait.</p>
              <SelectField label="Raison" value={reason} onChange={(e) => setReason(e.target.value as ReportReason)}>
                {REASONS.map((r) => (
                  <option key={r} value={r}>
                    {reportReasonLabel[r]}
                  </option>
                ))}
              </SelectField>
              <TextArea label="Précisions (facultatif)" value={details} maxLength={1000} onChange={(e) => setDetails(e.target.value)} />
              {error && <p className="alert">{error}</p>}
            </>
          )}
          <div className="form-actions">
            {!receipt && (
              <button type="submit" className="btn btn--action" disabled={pending}>
                Envoyer
              </button>
            )}
            <button type="button" className="btn btn--ghost" onClick={() => dialog.current?.close()}>
              Fermer
            </button>
          </div>
        </form>
      </dialog>
    </>
  )
}
