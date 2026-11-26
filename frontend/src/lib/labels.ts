import type {
  ExchangeKind,
  ExchangeStatus,
  ItemCondition,
  ListingDirection,
  ListingStatus,
  ListingType,
  ReportReason,
} from '../api/types'

/** Libellés français des valeurs de l'API. Centralisés ici pour préparer d'autres langues (bambara). */

export const directionLabel: Record<ListingDirection, string> = {
  Offer: 'Je propose',
  Request: 'Je cherche',
}

export const typeLabel: Record<ListingType, string> = {
  Good: 'Objet',
  Service: 'Service',
}

export const conditionLabel: Record<ItemCondition, string> = {
  New: 'Neuf',
  Good: 'Bon état',
  Used: 'Usé',
}

export const listingStatusLabel: Record<ListingStatus, string> = {
  Draft: 'Brouillon',
  Published: 'Publiée',
  Reserved: 'Réservée',
  Exchanged: 'Échangée',
  Archived: 'Archivée',
  Suspended: 'Suspendue',
}

export const kindLabel: Record<ExchangeKind, string> = {
  Barter: 'Troc',
  TimeBank: 'Banque de temps',
  Donation: 'Don',
}

export const exchangeStatusLabel: Record<ExchangeStatus, string> = {
  Proposed: 'En attente de réponse',
  Accepted: 'Accepté, à réaliser',
  Completed: 'Terminé',
  Declined: 'Refusé',
  Cancelled: 'Annulé',
  Expired: 'Expiré',
  Disputed: 'Litige en cours',
}

/** Ton visuel d'un statut : crédit (vert karité), débit (rouille), neutre ou encre. */
export const exchangeStatusTone: Record<ExchangeStatus, 'credit' | 'debit' | 'neutral' | 'ink'> = {
  Proposed: 'neutral',
  Accepted: 'ink',
  Completed: 'credit',
  Declined: 'debit',
  Cancelled: 'neutral',
  Expired: 'neutral',
  Disputed: 'debit',
}

export const reportReasonLabel: Record<ReportReason, string> = {
  NoLongerAvailable: 'Ce n’est plus disponible',
  Scam: 'Arnaque ou demande d’argent',
  Inappropriate: 'Contenu inapproprié',
  Prohibited: 'Objet interdit',
  Other: 'Autre raison',
}
