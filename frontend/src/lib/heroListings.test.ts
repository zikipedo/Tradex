import { describe, expect, it } from 'vitest'
import type { ListingSummary } from '../api/types'
import { pickHeroListings } from './heroListings'

function listing(id: string, direction: 'Offer' | 'Request', photo: boolean): ListingSummary {
  return {
    id,
    title: id,
    direction,
    type: 'Good',
    categoryName: 'Maison et jardin',
    city: 'Bamako',
    neighborhood: null,
    isDonation: false,
    status: 'Published',
    coverPhotoUrl: photo ? `/api/v1/listings/${id}/photos/p` : null,
    owner: { id: 'o', displayName: 'Awa' },
    publishedAt: null,
  }
}

describe('pickHeroListings', () => {
  it('montre une offre et une demande, en privilégiant celles qui ont une photo', () => {
    const picked = pickHeroListings([
      listing('offre-sans-photo', 'Offer', false),
      listing('demande-sans-photo', 'Request', false),
      listing('offre-avec-photo', 'Offer', true),
      listing('demande-avec-photo', 'Request', true),
    ])
    expect(picked.map((l) => l.id)).toEqual(['offre-avec-photo', 'demande-avec-photo'])
  })

  it('sans demande, prend les deux premières offres (photos d’abord)', () => {
    const picked = pickHeroListings([listing('a', 'Offer', false), listing('b', 'Offer', true), listing('c', 'Offer', false)])
    expect(picked.map((l) => l.id)).toEqual(['b', 'a'])
  })

  it('fonctionne avec une seule annonce ou aucune', () => {
    expect(pickHeroListings([listing('seule', 'Request', false)])).toHaveLength(1)
    expect(pickHeroListings([])).toEqual([])
  })
})
