import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DevMailNotice } from './DevMailNotice'

describe('DevMailNotice', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('en développement, indique où trouver l’e-mail', () => {
    vi.stubEnv('DEV', true)
    render(<DevMailNotice />)
    expect(screen.getByRole('note', { name: 'Rappel pour le développement' })).toHaveTextContent('src/TradX.Api/.mail-outbox')
  })

  it('en production, n’affiche rien', () => {
    vi.stubEnv('DEV', false)
    const { container } = render(<DevMailNotice />)
    expect(container).toBeEmptyDOMElement()
  })
})
