import { useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { exchangesApi, listingsApi } from '../../api/endpoints'
import type { ExchangeKind, Listing } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { SelectField, Segmented, TextArea } from '../../components/Field'
import { formatMinutes } from '../../lib/format'
import { useAction, useResource } from '../../lib/useAsync'
import { allowedKinds } from '../../lib/exchangeKinds'
import { TIME_RULES } from '../../lib/timeRules'

const DURATIONS = Array.from(
  { length: TIME_RULES.maxServiceMinutes / TIME_RULES.slotMinutes },
  (_, i) => (i + 1) * TIME_RULES.slotMinutes,
)

/** Panneau « Proposer un échange » sur la page d'une annonce. */
export function ProposePanel({ listing }: { listing: Listing }) {
  const { status, me } = useAuth()
  const navigate = useNavigate()
  const kinds = useMemo(() => allowedKinds(listing), [listing])
  const [kind, setKind] = useState<ExchangeKind>(kinds[0]!)
  const [offeredId, setOfferedId] = useState('')
  const [duration, setDuration] = useState(60)
  const [message, setMessage] = useState('')
  const mine = useResource(
    () => (status === 'member' && kinds.includes('Barter') ? listingsApi.mine() : Promise.resolve(null)),
    [status, kinds],
  )
  const offerable = (mine.data?.items ?? []).filter((l) => l.status === 'Published' && l.id !== listing.id)
  const { run, pending, error, fieldErrors } = useAction(exchangesApi.propose)

  if (status !== 'member') {
    return (
      <div className="panel panel--kaolin propose">
        <h2 className="h3">Ça t’intéresse ?</h2>
        <p className="muted">Crée un compte gratuit pour proposer un échange. Aucun paiement, jamais.</p>
        <div className="form-actions">
          <Link to="/inscription" className="btn btn--action">
            Créer un compte
          </Link>
          <Link to="/connexion" state={{ from: `/annonces/${listing.id}` }} className="btn btn--ghost">
            Se connecter
          </Link>
        </div>
      </div>
    )
  }

  if (me && !me.emailConfirmed) {
    return (
      <div className="panel panel--kaolin propose">
        <h2 className="h3">Confirme d’abord ton e-mail.</h2>
        <p className="muted">C’est la règle pour échanger : elle protège tout le monde des faux comptes.</p>
        <Link to="/moi" className="btn">
          Renvoyer le lien
        </Link>
      </div>
    )
  }

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const exchange = await run({
      targetListingId: listing.id,
      kind,
      offeredListingId: kind === 'Barter' ? offeredId : null,
      durationMinutes: kind === 'TimeBank' ? duration : null,
      message: message.trim() || null,
    })
    if (exchange) navigate(`/echanges/${exchange.id}`)
  }

  // Qui rend le service dans la banque de temps : l'auteur s'il propose, le demandeur s'il cherche.
  const providerIsOwner = listing.direction === 'Offer'
  const balanceAfter =
    me && kind === 'TimeBank' ? me.timeBalanceMinutes + (providerIsOwner ? -duration : duration) : null
  // Même contrôle que l'API à la proposition (seul mon solde est connu ici ; l'API vérifie aussi celui de l'autre).
  const outOfBounds =
    balanceAfter !== null && (balanceAfter < TIME_RULES.overdraftLimitMinutes || balanceAfter > TIME_RULES.maxBalanceMinutes)

  return (
    <form className="panel propose" onSubmit={onSubmit} noValidate>
      <h2 className="h3">Proposer un échange</h2>
      {kinds.length > 1 && (
        <Segmented
          legend="Comment ?"
          name="kind"
          value={kind}
          onChange={setKind}
          options={kinds.map((k) =>
            k === 'TimeBank'
              ? { value: k, label: 'En heures', hint: 'Banque de temps' }
              : { value: k, label: 'En troc', hint: 'Contre une de mes annonces' },
          )}
        />
      )}
      {kind === 'Donation' && <p className="muted">C’est un don : tu le demandes sans rien donner en retour. Un petit mot fait plaisir.</p>}

      {kind === 'Barter' &&
        (offerable.length > 0 ? (
          <SelectField label="Ce que je propose en retour" value={offeredId} onChange={(e) => setOfferedId(e.target.value)} required error={fieldErrors.offeredlistingid}>
            <option value="">Choisir une de mes annonces publiées</option>
            {offerable.map((l) => (
              <option key={l.id} value={l.id}>
                {l.title}
              </option>
            ))}
          </SelectField>
        ) : (
          <p className="alert">
            Pour un troc, il te faut une annonce publiée à proposer en retour. <Link to="/publier">Publier une annonce</Link>
          </p>
        ))}

      {kind === 'TimeBank' && (
        <>
          <SelectField label="Durée du service" value={duration} onChange={(e) => setDuration(Number(e.target.value))} error={fieldErrors.durationminutes}>
            {DURATIONS.map((d) => (
              <option key={d} value={d}>
                {formatMinutes(d)}
              </option>
            ))}
          </SelectField>
          {balanceAfter !== null && (
            <p className={outOfBounds ? 'alert' : 'muted'}>
              {providerIsOwner ? 'Tu reçois le service.' : 'C’est toi qui rends le service.'} Ton solde passerait de{' '}
              <strong>{formatMinutes(me!.timeBalanceMinutes, { signed: true })}</strong> à{' '}
              <strong>{formatMinutes(balanceAfter, { signed: true })}</strong>
              {balanceAfter < TIME_RULES.overdraftLimitMinutes
                ? ' : c’est sous le découvert autorisé de −5 h. Rends d’abord service à quelqu’un, ou choisis une durée plus courte.'
                : balanceAfter > TIME_RULES.maxBalanceMinutes
                  ? ' : c’est au-dessus du plafond de 100 h. Dépense d’abord une partie de tes heures.'
                  : ', une fois l’échange confirmé par vous deux.'}
            </p>
          )}
        </>
      )}

      <TextArea
        label="Un mot pour te présenter"
        value={message}
        maxLength={500}
        onChange={(e) => setMessage(e.target.value)}
        placeholder="Bonjour, je suis à Kalaban Coura, disponible le week-end…"
        hint={`${message.length} / 500`}
        error={fieldErrors.message}
      />
      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}
      <button
        type="submit"
        className="btn btn--action"
        disabled={pending || (kind === 'Barter' && !offeredId) || outOfBounds}
      >
        {pending ? 'Envoi…' : 'Envoyer la proposition'}
      </button>
      <p className="field-hint">L’autre a sept jours pour répondre. Rien n’est engagé tant que vous n’avez pas confirmé tous les deux.</p>
    </form>
  )
}
