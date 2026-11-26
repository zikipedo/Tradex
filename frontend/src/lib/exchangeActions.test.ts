import { describe, expect, it } from 'vitest'
import type { Exchange, ExchangeStatus } from '../api/types'
import { counterpart, exchangeActions } from './exchangeActions'
import { allowedKinds } from './exchangeKinds'

const OWNER = 'owner-id'
const REQUESTER = 'requester-id'
const STRANGER = 'stranger-id'
const NOW = new Date('2026-11-26T12:00:00Z')

function exchange(status: ExchangeStatus, overrides: Partial<Exchange> = {}): Exchange {
  return {
    id: 'x',
    kind: 'Barter',
    status,
    requester: { id: REQUESTER, displayName: 'Moussa' },
    owner: { id: OWNER, displayName: 'Awa' },
    targetListing: { id: 'l1', title: 'Perceuse', type: 'Good', status: 'Published' },
    offeredListing: { id: 'l2', title: 'Vélo', type: 'Good', status: 'Published' },
    durationMinutes: null,
    providerId: null,
    beneficiaryId: null,
    message: null,
    createdAt: '2026-11-25T12:00:00Z',
    expiresAt: '2026-12-02T12:00:00Z',
    respondedAt: null,
    requesterConfirmedAt: null,
    ownerConfirmedAt: null,
    closedAt: null,
    disputeReason: null,
    resolutionNote: null,
    ...overrides,
  }
}

describe('exchangeActions : proposition', () => {
  it('seul l’auteur de l’annonce accepte ou refuse', () => {
    expect(exchangeActions(exchange('Proposed'), OWNER, NOW)).toMatchObject({ accept: true, decline: true, cancel: false })
    expect(exchangeActions(exchange('Proposed'), REQUESTER, NOW)).toMatchObject({ accept: false, decline: false, cancel: true })
  })

  it('une proposition expirée ne peut plus être acceptée', () => {
    const expired = exchange('Proposed', { expiresAt: '2026-11-26T11:00:00Z' })
    expect(exchangeActions(expired, OWNER, NOW).accept).toBe(false)
  })

  it('un inconnu n’a aucune action', () => {
    const actions = exchangeActions(exchange('Accepted'), STRANGER, NOW)
    expect(Object.values(actions).every((v) => v === false)).toBe(true)
  })
})

describe('exchangeActions : échange accepté', () => {
  it('les deux parties peuvent confirmer, annuler et ouvrir un litige', () => {
    for (const me of [OWNER, REQUESTER]) {
      expect(exchangeActions(exchange('Accepted'), me, NOW)).toMatchObject({ confirm: true, cancel: true, dispute: true })
    }
  })

  it('après une confirmation, plus personne ne peut annuler seul', () => {
    const halfConfirmed = exchange('Accepted', { ownerConfirmedAt: '2026-11-26T10:00:00Z' })
    expect(exchangeActions(halfConfirmed, OWNER, NOW)).toMatchObject({ confirm: false, cancel: false, waitingForOther: true })
    expect(exchangeActions(halfConfirmed, REQUESTER, NOW)).toMatchObject({ confirm: true, cancel: false, waitingForOther: false })
  })
})

describe('exchangeActions : fin de l’échange', () => {
  it('un échange terminé permet seulement l’avis', () => {
    expect(exchangeActions(exchange('Completed'), REQUESTER, NOW)).toEqual({
      accept: false,
      decline: false,
      cancel: false,
      confirm: false,
      dispute: false,
      review: true,
      waitingForOther: false,
    })
  })

  it.each<ExchangeStatus>(['Declined', 'Cancelled', 'Expired', 'Disputed'])('« %s » ne propose aucune action aux parties', (status) => {
    const actions = exchangeActions(exchange(status), OWNER, NOW)
    expect(Object.values(actions).some(Boolean)).toBe(false)
  })
})

describe('counterpart', () => {
  it('renvoie toujours l’autre membre', () => {
    expect(counterpart(exchange('Accepted'), OWNER).displayName).toBe('Moussa')
    expect(counterpart(exchange('Accepted'), REQUESTER).displayName).toBe('Awa')
  })
})

describe('allowedKinds (règles de Exchange.Propose)', () => {
  it('un don se demande uniquement comme un don', () => {
    expect(allowedKinds({ isDonation: true, type: 'Good' })).toEqual(['Donation'])
    expect(allowedKinds({ isDonation: true, type: 'Service' })).toEqual(['Donation'])
  })

  it('la banque de temps est réservée aux services', () => {
    expect(allowedKinds({ isDonation: false, type: 'Service' })).toEqual(['TimeBank', 'Barter'])
    expect(allowedKinds({ isDonation: false, type: 'Good' })).toEqual(['Barter'])
  })
})
