import type { ListingSummary } from '../api/types'

/**
 * Choisit les deux annonces réelles montrées dans le héros de l'accueil :
 * d'abord celles qui ont une photo, et si possible une offre (« je propose ») et une demande (« je cherche »)
 * pour illustrer les deux sens de l'échange. Renvoie au plus deux annonces, dans l'ordre d'affichage.
 */
export function pickHeroListings(items: ListingSummary[]): ListingSummary[] {
  // Tri stable : les annonces avec photo passent devant, l'ordre de publication est conservé sinon.
  const sorted = [...items].sort((a, b) => Number(Boolean(b.coverPhotoUrl)) - Number(Boolean(a.coverPhotoUrl)))
  const offer = sorted.find((l) => l.direction === 'Offer')
  const request = sorted.find((l) => l.direction === 'Request')
  if (offer && request) return [offer, request]
  return sorted.slice(0, 2)
}
