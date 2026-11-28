import type { ListingSummary } from '../api/types'

/**
 * Place les annonces qui ont une photo devant les autres, sans changer l'ordre à l'intérieur de chaque groupe
 * (le tri est stable, donc l'ordre de publication est conservé). Sert aux vitrines de l'accueil,
 * où une grande carte sans photo paraît vide.
 */
export function withPhotosFirst(items: ListingSummary[]): ListingSummary[] {
  return [...items].sort((a, b) => Number(Boolean(b.coverPhotoUrl)) - Number(Boolean(a.coverPhotoUrl)))
}

/**
 * Choisit les deux annonces réelles montrées dans le héros de l'accueil :
 * d'abord celles qui ont une photo, et si possible une offre (« je propose ») et une demande (« je cherche »)
 * pour illustrer les deux sens de l'échange. Renvoie au plus deux annonces, dans l'ordre d'affichage.
 */
export function pickHeroListings(items: ListingSummary[]): ListingSummary[] {
  const sorted = withPhotosFirst(items)
  const offer = sorted.find((l) => l.direction === 'Offer')
  const request = sorted.find((l) => l.direction === 'Request')
  if (offer && request) return [offer, request]
  return sorted.slice(0, 2)
}
