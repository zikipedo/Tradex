import type { ExchangeKind, Listing } from '../api/types'

/**
 * Les façons d'échanger possibles pour une annonce, selon les règles du domaine (`Exchange.Propose`) :
 * un don se demande comme un don ; sinon le troc est toujours possible, et la banque de temps
 * seulement pour un service.
 */
export function allowedKinds(listing: Pick<Listing, 'isDonation' | 'type'>): ExchangeKind[] {
  if (listing.isDonation) return ['Donation']
  return listing.type === 'Service' ? ['TimeBank', 'Barter'] : ['Barter']
}
