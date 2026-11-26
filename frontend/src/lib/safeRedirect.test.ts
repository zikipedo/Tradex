import { describe, expect, it } from 'vitest'
import { safeRedirect } from './safeRedirect'

describe('safeRedirect (protection contre la redirection ouverte)', () => {
  it.each(['/echanges', '/annonces/0193f0a0-0000-7000-8000-000000000001', '/annonces?type=Service&q=v%C3%A9lo'])(
    'accepte le chemin interne %s',
    (path) => {
      expect(safeRedirect(path)).toBe(path)
    },
  )

  it.each([
    'https://site-piege.example',
    '//site-piege.example/connexion',
    '/\\site-piege.example',
    'javascript:alert(1)',
    'echanges',
    '/annonces\n/x',
    '',
  ])('refuse %j et renvoie vers les annonces', (target) => {
    expect(safeRedirect(target)).toBe('/annonces')
  })

  it('refuse les valeurs qui ne sont pas du texte', () => {
    expect(safeRedirect(undefined)).toBe('/annonces')
    expect(safeRedirect({ pathname: '/x' })).toBe('/annonces')
    expect(safeRedirect(null, '/')).toBe('/')
  })
})
