import type { ReactNode } from 'react'
import { MotifBand, type MotifName } from '../../components/Motif'

/**
 * Mise en page des écrans de compte : à gauche une bande sombre avec une phrase qui rappelle
 * le principe de TradX, à droite le formulaire. Empilés sur mobile.
 */
export function AuthShell({ quote, motif = 'zigzag', children }: { quote: ReactNode; motif?: MotifName; children: ReactNode }) {
  return (
    <div className="auth">
      <aside className="auth__aside on-ink">
        <MotifBand name={motif} height={24} className="band" />
        <p className="display">{quote}</p>
        <p className="label" style={{ color: 'var(--text-3)' }}>
          TradX · Bamako · sans argent
        </p>
      </aside>
      <div className="auth__main">{children}</div>
    </div>
  )
}
