import { formatMinutes } from '../lib/format'
import './TimeStrip.css'

/** Une maille = 30 minutes, l'unité de la banque de temps. */
export const SLOT_MINUTES = 30

export interface TimeStripProps {
  balanceMinutes: number
  overdraftLimitMinutes: number
  maxBalanceMinutes: number
  /** Nombre de mailles visibles côté positif (au-delà, un compteur « + n h » s'affiche). */
  visiblePositiveSlots?: number
  /** Variante sombre (sur fond encre). */
  dark?: boolean
}

/**
 * Le solde de temps dessiné comme une bande tissée.
 * À gauche du nœud central : le découvert possible (mailles rouille quand on doit du temps).
 * À droite : le temps donné aux autres (mailles vert karité).
 * Le nombre de mailles et leur couleur portent l'information ; le texte la répète pour les lecteurs d'écran.
 */
export function TimeStrip({
  balanceMinutes,
  overdraftLimitMinutes,
  maxBalanceMinutes,
  visiblePositiveSlots = 20,
  dark = false,
}: TimeStripProps) {
  const debtSlots = Math.round(Math.abs(overdraftLimitMinutes) / SLOT_MINUTES)
  const filledDebt = balanceMinutes < 0 ? Math.ceil(-balanceMinutes / SLOT_MINUTES) : 0
  const filledCredit = balanceMinutes > 0 ? Math.floor(balanceMinutes / SLOT_MINUTES) : 0
  const overflow = Math.max(0, filledCredit - visiblePositiveSlots)

  const description =
    `Solde : ${formatMinutes(balanceMinutes)}. ` +
    `Découvert possible jusqu’à ${formatMinutes(overdraftLimitMinutes)}, plafond ${formatMinutes(maxBalanceMinutes)}.`

  return (
    <div className={`timestrip${dark ? ' timestrip--dark' : ''}`} role="img" aria-label={description}>
      <div className="timestrip__side timestrip__side--debt">
        {Array.from({ length: debtSlots }, (_, i) => {
          // On remplit depuis le nœud vers la gauche.
          const filled = debtSlots - i <= filledDebt
          return <span key={i} className={`stitch stitch--debt${filled ? ' is-filled' : ''}`} />
        })}
      </div>
      <span className="timestrip__knot" aria-hidden="true" />
      <div className="timestrip__side timestrip__side--credit">
        {Array.from({ length: visiblePositiveSlots }, (_, i) => (
          <span key={i} className={`stitch stitch--credit${i < filledCredit ? ' is-filled' : ''}`} />
        ))}
        {overflow > 0 && <span className="timestrip__more">+{formatMinutes(overflow * SLOT_MINUTES)}</span>}
      </div>
    </div>
  )
}
