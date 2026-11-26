import { describe, expect, it } from 'vitest'
import { formatMinutes, formatRelative, initials, plural } from './format'

describe('formatMinutes', () => {
  it.each([
    [0, '0 min'],
    [30, '30 min'],
    [60, '1 h'],
    [90, '1 h 30'],
    [480, '8 h'],
    [-300, '−5 h'],
    [-30, '−30 min'],
    [6000, '100 h'],
  ])('%i minutes → « %s »', (minutes, expected) => {
    expect(formatMinutes(minutes)).toBe(expected)
  })

  it('ajoute le signe plus seulement si demandé et si le solde est positif', () => {
    expect(formatMinutes(150, { signed: true })).toBe('+2 h 30')
    expect(formatMinutes(0, { signed: true })).toBe('0 min')
    expect(formatMinutes(-60, { signed: true })).toBe('−1 h')
  })
})

describe('formatRelative', () => {
  const now = new Date('2026-11-26T12:00:00Z')

  it.each([
    ['2026-11-26T11:59:30Z', 'à l’instant'],
    ['2026-11-26T11:45:00Z', 'il y a 15 min'],
    ['2026-11-26T09:00:00Z', 'il y a 3 h'],
    ['2026-11-24T12:00:00Z', 'il y a 2 j'],
  ])('%s → « %s »', (iso, expected) => {
    expect(formatRelative(iso, now)).toBe(expected)
  })

  it('affiche la date au-delà d’une semaine', () => {
    expect(formatRelative('2026-10-01T12:00:00Z', now)).toMatch(/oct\.? 2026/)
  })
})

describe('initials et pluriels', () => {
  it('prend les deux premières initiales', () => {
    expect(initials('Awa Traoré')).toBe('AT')
    expect(initials('  moussa   keïta  diarra ')).toBe('MK')
    expect(initials('Fanta')).toBe('F')
  })

  it('accorde le nom avec le nombre', () => {
    expect(plural(0, 'annonce')).toBe('0 annonce')
    expect(plural(1, 'annonce')).toBe('1 annonce')
    expect(plural(3, 'avis', 'avis')).toBe('3 avis')
    expect(plural(1200, 'annonce')).toMatch(/^1\s200 annonces$/)
  })
})
