import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import type { ListingSummary } from '../api/types'
import { TimeDemo } from '../pages/home/TimeDemo'
import { ListingCard } from './ListingCard'
import { TimeStrip } from './TimeStrip'

const RULES = { overdraftLimitMinutes: -300, maxBalanceMinutes: 6000 }

describe('TimeStrip', () => {
  it('remplit une maille verte par demi-heure donnée et décrit le solde en texte', () => {
    const { container } = render(<TimeStrip balanceMinutes={150} {...RULES} />)
    expect(container.querySelectorAll('.stitch--credit.is-filled')).toHaveLength(5)
    expect(container.querySelectorAll('.stitch--debt.is-filled')).toHaveLength(0)
    expect(screen.getByRole('img')).toHaveAccessibleName('Solde : 2 h 30. Découvert possible jusqu’à −5 h, plafond 100 h.')
  })

  it('montre la dette en mailles rouille, depuis le nœud, dans la limite du découvert', () => {
    const { container } = render(<TimeStrip balanceMinutes={-90} {...RULES} />)
    const debt = [...container.querySelectorAll('.stitch--debt')]
    expect(debt).toHaveLength(10)
    expect(debt.filter((s) => s.classList.contains('is-filled'))).toHaveLength(3)
    // Les mailles remplies sont les plus proches du nœud central (à droite de la zone de dette).
    expect(debt.slice(-3).every((s) => s.classList.contains('is-filled'))).toBe(true)
  })

  it('indique le surplus au-delà des mailles visibles', () => {
    render(<TimeStrip balanceMinutes={1500} {...RULES} visiblePositiveSlots={20} />)
    expect(screen.getByText('+15 h')).toBeInTheDocument()
  })
})

describe('TimeDemo (règles de la banque de temps)', () => {
  it('accepte un service tant que le receveur reste au-dessus de −5 h', () => {
    render(<TimeDemo />)
    fireEvent.change(screen.getByLabelText('Durée du cours de couture'), { target: { value: '300' } })
    expect(screen.getByRole('status')).toHaveTextContent('Échange possible')
  })

  it('refuse un service qui ferait passer le receveur sous −5 h', () => {
    render(<TimeDemo />)
    fireEvent.change(screen.getByLabelText('Durée du cours de couture'), { target: { value: '330' } })
    expect(screen.getByRole('status')).toHaveTextContent('Refusé : Moussa passerait à −5 h 30')
  })

  it('n’autorise que des tranches de 30 minutes, de 30 min à 8 h', () => {
    render(<TimeDemo />)
    const slider = screen.getByLabelText('Durée du cours de couture')
    expect(slider).toHaveAttribute('min', '30')
    expect(slider).toHaveAttribute('max', '480')
    expect(slider).toHaveAttribute('step', '30')
  })
})

describe('ListingCard', () => {
  const listing: ListingSummary = {
    id: '0193f0a0-0000-7000-8000-000000000001',
    title: 'Machine à coudre Singer',
    direction: 'Offer',
    type: 'Good',
    categoryName: 'Maison',
    city: 'Bamako',
    neighborhood: 'Badalabougou',
    isDonation: true,
    status: 'Published',
    coverPhotoUrl: null,
    owner: { id: 'o1', displayName: 'Awa T.' },
    publishedAt: null,
  }

  it('n’expose qu’un seul lien, vers la page de l’annonce', () => {
    render(
      <MemoryRouter>
        <ListingCard listing={listing} />
      </MemoryRouter>,
    )
    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(1)
    expect(links[0]).toHaveAttribute('href', `/annonces/${listing.id}`)
    expect(links[0]).toHaveAccessibleName('Machine à coudre Singer')
  })

  it('affiche le sens, le don et le lieu, et aucun prix', () => {
    const { container } = render(
      <MemoryRouter>
        <ListingCard listing={listing} />
      </MemoryRouter>,
    )
    expect(screen.getByText('Je propose')).toBeInTheDocument()
    expect(screen.getByText('Don')).toBeInTheDocument()
    expect(screen.getByText('Badalabougou, Bamako')).toBeInTheDocument()
    expect(container.textContent).not.toMatch(/F CFA|FCFA|€|\$|prix/i)
  })

  it('affiche le texte utilisateur tel quel, sans interpréter de HTML', () => {
    const { container } = render(
      <MemoryRouter>
        <ListingCard listing={{ ...listing, title: '<img src=x onerror=alert(1)>' }} />
      </MemoryRouter>,
    )
    expect(container.querySelector('img[src="x"]')).toBeNull()
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeInTheDocument()
  })
})
