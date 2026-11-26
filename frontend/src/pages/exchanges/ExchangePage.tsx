import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { exchangesApi } from '../../api/endpoints'
import type { Exchange } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { TextArea } from '../../components/Field'
import { ErrorNote, Loader } from '../../components/Loader'
import { counterpart, exchangeActions } from '../../lib/exchangeActions'
import { formatDate, formatMinutes } from '../../lib/format'
import { exchangeStatusLabel, exchangeStatusTone, kindLabel } from '../../lib/labels'
import { useAction, useResource } from '../../lib/useAsync'
import { Conversation } from './Conversation'
import { ExchangeKnot } from './ExchangeKnot'
import './ExchangePage.css'

/** Détail d'un échange : ce que chacun apporte, les actions possibles, la conversation et l'avis. */
export default function ExchangePage() {
  const { id = '' } = useParams()
  const { me, reloadMe } = useAuth()
  const { data: exchange, setData, error, loading, reload } = useResource(() => exchangesApi.get(id), [id])

  const act = useAction(async (action: 'accept' | 'decline' | 'cancel' | 'confirm') => {
    const updated = await exchangesApi[action](id)
    setData(updated)
    // Un échange en heures terminé change les soldes : on rafraîchit le compteur de l'en-tête.
    if (updated.status === 'Completed') await reloadMe()
    return updated
  })

  if (loading && !exchange) return <Loader label="Chargement de l’échange" />
  if (error || !exchange || !me)
    return (
      <div className="wrap page">
        <ErrorNote message={error ?? 'Échange introuvable.'} onRetry={reload} />
      </div>
    )

  const actions = exchangeActions(exchange, me.id)
  const other = counterpart(exchange, me.id)
  const iAmOwner = exchange.owner.id === me.id

  return (
    <div className="xpage wrap page">
      <Link to="/echanges" className="back-link">
        <span aria-hidden="true">←</span> Mes échanges
      </Link>

      <header className="xpage__head">
        <div>
          <p className="label muted">
            {kindLabel[exchange.kind]} · proposé le {formatDate(exchange.createdAt)}
          </p>
          <h1 className="h1">
            {exchange.targetListing.title}
          </h1>
        </div>
        <span className={`pill pill--${exchangeStatusTone[exchange.status]} xpage__status`}>{exchangeStatusLabel[exchange.status]}</span>
      </header>

      <ExchangeKnot status={exchange.status} leftName={exchange.requester.displayName} rightName={exchange.owner.displayName} />

      <div className="xpage__grid">
        <div className="xpage__main">
          <Terms exchange={exchange} myId={me.id} />

          {exchange.message && (
            <blockquote className="xpage__note">
              <p>« {exchange.message} »</p>
              <cite>{exchange.requester.displayName}, en proposant</cite>
            </blockquote>
          )}

          {act.error && (
            <p className="alert" role="alert">
              {act.error}
            </p>
          )}

          <div className="form-actions">
            {actions.accept && (
              <button type="button" className="btn btn--action" disabled={act.pending} onClick={() => act.run('accept')}>
                Accepter
              </button>
            )}
            {actions.confirm && (
              <button type="button" className="btn btn--action" disabled={act.pending} onClick={() => act.run('confirm')}>
                C’est fait, je confirme
              </button>
            )}
            {actions.decline && (
              <button type="button" className="btn" disabled={act.pending} onClick={() => act.run('decline')}>
                Refuser
              </button>
            )}
            {actions.cancel && (
              <button
                type="button"
                className="btn btn--ghost"
                disabled={act.pending}
                onClick={() => window.confirm('Annuler cet échange ?') && act.run('cancel')}
              >
                Annuler l’échange
              </button>
            )}
          </div>
          {actions.waitingForOther && <p className="alert alert--ok">Ta confirmation est enregistrée. L’échange se termine quand {other.displayName} confirme aussi.</p>}
          {exchange.status === 'Proposed' && !iAmOwner && (
            <p className="field-hint">
              {other.displayName} a jusqu’au {formatDate(exchange.expiresAt)} pour répondre.
            </p>
          )}
          {exchange.status === 'Disputed' && (
            <p className="alert">
              Litige ouvert : « {exchange.disputeReason} ». Un modérateur va trancher.
            </p>
          )}
          {exchange.resolutionNote && <p className="alert alert--ok">Décision de la modération : {exchange.resolutionNote}</p>}

          {actions.review && <ReviewForm exchangeId={exchange.id} otherName={other.displayName} />}
          {actions.dispute && <DisputeForm exchangeId={exchange.id} onDone={setData} />}
        </div>

        <Conversation exchangeId={exchange.id} myId={me.id} open={exchange.status !== 'Declined' && exchange.status !== 'Expired'} />
      </div>
    </div>
  )
}

/** Ce que chacun apporte, écrit en clair. */
function Terms({ exchange, myId }: { exchange: Exchange; myId: string }) {
  /** Sujet conjugué : « Tu donnes » pour moi, « Awa donne » pour l'autre. */
  const says = (id: string | null, mine: string, theirs: string) => {
    if (id === myId) return `Tu ${mine}`
    const name = id === exchange.owner.id ? exchange.owner.displayName : exchange.requester.displayName
    return `${name} ${theirs}`
  }
  return (
    <dl className="terms">
      {exchange.kind === 'Barter' && exchange.offeredListing && (
        <>
          <div>
            <dt>{says(exchange.owner.id, 'donnes', 'donne')}</dt>
            <dd>
              <Link to={`/annonces/${exchange.targetListing.id}`}>{exchange.targetListing.title}</Link>
            </dd>
          </div>
          <div>
            <dt>{says(exchange.requester.id, 'donnes', 'donne')}</dt>
            <dd>
              <Link to={`/annonces/${exchange.offeredListing.id}`}>{exchange.offeredListing.title}</Link>
            </dd>
          </div>
        </>
      )}
      {exchange.kind === 'TimeBank' && exchange.durationMinutes && (
        <>
          <div>
            <dt>{says(exchange.providerId, 'rends le service', 'rend le service')}</dt>
            <dd>
              {exchange.targetListing.title} · {formatMinutes(exchange.durationMinutes)}
            </dd>
          </div>
          <div>
            <dt>{says(exchange.beneficiaryId, 'donnes en retour', 'donne en retour')}</dt>
            <dd>
              {formatMinutes(exchange.durationMinutes)} de temps,{' '}
              {exchange.status === 'Completed' ? 'transférées à la fin de l’échange' : 'transférées quand vous aurez confirmé tous les deux'}
            </dd>
          </div>
        </>
      )}
      {exchange.kind === 'Donation' && (
        <div>
          <dt>{says(exchange.owner.id, 'donnes', 'donne')}</dt>
          <dd>
            <Link to={`/annonces/${exchange.targetListing.id}`}>{exchange.targetListing.title}</Link>, sans contrepartie
          </dd>
        </div>
      )}
    </dl>
  )
}

function ReviewForm({ exchangeId, otherName }: { exchangeId: string; otherName: string }) {
  const [rating, setRating] = useState(5)
  const [comment, setComment] = useState('')
  const [done, setDone] = useState(false)
  const { run, pending, error } = useAction(exchangesApi.review)

  if (done) return <p className="alert alert--ok">Merci, ton avis est publié sur le profil de {otherName}.</p>

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (await run(exchangeId, rating, comment.trim() || null)) setDone(true)
  }

  return (
    <form className="panel panel--kaolin review" onSubmit={onSubmit}>
      <h2 className="h3">Comment ça s’est passé avec {otherName} ?</h2>
      <fieldset className="stars">
        <legend className="sr-only">Note sur 5</legend>
        {[1, 2, 3, 4, 5].map((n) => (
          <label key={n} className={n <= rating ? 'is-on' : ''}>
            <input type="radio" name="rating" value={n} checked={rating === n} onChange={() => setRating(n)} />
            <span aria-hidden="true">●</span>
            <span className="sr-only">{n} sur 5</span>
          </label>
        ))}
      </fieldset>
      <TextArea label="Un commentaire (facultatif)" value={comment} maxLength={1000} onChange={(e) => setComment(e.target.value)} rows={3} />
      {error && <p className="alert">{error}</p>}
      <button type="submit" className="btn btn--action" disabled={pending}>
        Publier mon avis
      </button>
    </form>
  )
}

function DisputeForm({ exchangeId, onDone }: { exchangeId: string; onDone: (e: Exchange) => void }) {
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const { run, pending, error, fieldErrors } = useAction(exchangesApi.dispute)

  if (!open)
    return (
      <button type="button" className="report-link" onClick={() => setOpen(true)}>
        Un problème ? Ouvrir un litige
      </button>
    )

  return (
    <form
      className="panel"
      onSubmit={async (event) => {
        event.preventDefault()
        const updated = await run(exchangeId, reason.trim())
        if (updated) onDone(updated)
      }}
    >
      <h2 className="h3">Ouvrir un litige</h2>
      <p className="muted">Un modérateur lira la conversation et tranchera : échange réalisé ou annulé.</p>
      <TextArea
        label="Que s’est-il passé ?"
        value={reason}
        minLength={10}
        maxLength={1000}
        onChange={(e) => setReason(e.target.value)}
        hint="10 caractères minimum."
        error={fieldErrors.reason}
      />
      {error && <p className="alert">{error}</p>}
      <div className="form-actions">
        <button type="submit" className="btn btn--action" disabled={pending || reason.trim().length < 10}>
          Envoyer à la modération
        </button>
        <button type="button" className="btn btn--ghost" onClick={() => setOpen(false)}>
          Annuler
        </button>
      </div>
    </form>
  )
}
