import type { Exchange } from '../api/types'

export interface ExchangeActions {
  accept: boolean
  decline: boolean
  cancel: boolean
  confirm: boolean
  dispute: boolean
  review: boolean
  /** Ma confirmation est déjà enregistrée, on attend l'autre. */
  waitingForOther: boolean
}

/**
 * Actions proposées à un membre sur un échange : reflet exact des règles du domaine (`Exchange.cs`).
 * C'est un confort d'affichage : l'API refuse de toute façon une action interdite.
 * - accepter / refuser : l'auteur de l'annonce, tant que la proposition n'a pas expiré ;
 * - annuler : le demandeur tant que c'est proposé ; les deux une fois accepté, si personne n'a confirmé ;
 * - confirmer : les deux parties, une fois accepté ;
 * - litige : les deux parties, une fois accepté ;
 * - avis : les deux parties, une fois terminé.
 */
export function exchangeActions(exchange: Exchange, myId: string, now: Date = new Date()): ExchangeActions {
  const isOwner = exchange.owner.id === myId
  const isRequester = exchange.requester.id === myId
  const isParty = isOwner || isRequester
  const myConfirmation = isOwner ? exchange.ownerConfirmedAt : isRequester ? exchange.requesterConfirmedAt : null
  const anyConfirmation = exchange.ownerConfirmedAt !== null || exchange.requesterConfirmedAt !== null
  const proposed = exchange.status === 'Proposed'
  const accepted = exchange.status === 'Accepted'
  const notExpired = new Date(exchange.expiresAt).getTime() > now.getTime()

  return {
    accept: proposed && isOwner && notExpired,
    decline: proposed && isOwner,
    cancel: (proposed && isRequester) || (accepted && isParty && !anyConfirmation),
    confirm: accepted && isParty && myConfirmation === null,
    dispute: accepted && isParty,
    review: exchange.status === 'Completed' && isParty,
    waitingForOther: accepted && myConfirmation !== null,
  }
}

/** L'autre membre de l'échange, vu par moi. */
export function counterpart(exchange: Exchange, myId: string) {
  return exchange.owner.id === myId ? exchange.requester : exchange.owner
}
