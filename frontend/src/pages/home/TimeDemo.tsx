import { useId, useState } from 'react'
import { TimeStrip } from '../../components/TimeStrip'
import { formatMinutes } from '../../lib/format'
import { TIME_RULES } from '../../lib/timeRules'

/**
 * Démonstration interactive : Awa donne un cours de couture à Moussa.
 * Le curseur règle la durée (30 min à 8 h, par pas de 30 min) ; les deux bandes tissées
 * montrent ce que chacun gagne ou doit, et la règle du découvert (−5 h) s'applique en direct.
 */
export function TimeDemo() {
  const [minutes, setMinutes] = useState(120)
  const sliderId = useId()
  const awaStart = 60
  const moussaStart = 0
  const moussaAfter = moussaStart - minutes
  const blocked = moussaAfter < TIME_RULES.overdraftLimitMinutes
  const awaAfter = blocked ? awaStart : awaStart + minutes
  const moussaShown = blocked ? moussaStart : moussaAfter

  return (
    <div className="timedemo">
      <div className="timedemo__control">
        <label htmlFor={sliderId} className="label">
          Durée du cours de couture
        </label>
        {/* Affichage visuel seulement : le curseur annonce déjà sa valeur (aria-valuetext). */}
        <output htmlFor={sliderId} className="timedemo__value tabular" aria-hidden="true">
          {formatMinutes(minutes)}
        </output>
        <input
          id={sliderId}
          type="range"
          min={TIME_RULES.slotMinutes}
          max={TIME_RULES.maxServiceMinutes}
          step={TIME_RULES.slotMinutes}
          value={minutes}
          onChange={(event) => setMinutes(Number(event.target.value))}
          aria-valuetext={formatMinutes(minutes)}
        />
      </div>

      <div className="timedemo__people">
        <figure className="timedemo__person">
          <figcaption>
            <span className="timedemo__name">Awa</span>
            <span className="muted"> donne le cours</span>
            <span className="timedemo__balance tabular">{formatMinutes(awaAfter, { signed: true })}</span>
          </figcaption>
          <TimeStrip balanceMinutes={awaAfter} overdraftLimitMinutes={TIME_RULES.overdraftLimitMinutes} maxBalanceMinutes={TIME_RULES.maxBalanceMinutes} dark />
        </figure>
        <figure className="timedemo__person">
          <figcaption>
            <span className="timedemo__name">Moussa</span>
            <span className="muted"> reçoit le cours</span>
            <span className="timedemo__balance tabular">{formatMinutes(moussaShown, { signed: true })}</span>
          </figcaption>
          <TimeStrip balanceMinutes={moussaShown} overdraftLimitMinutes={TIME_RULES.overdraftLimitMinutes} maxBalanceMinutes={TIME_RULES.maxBalanceMinutes} dark />
        </figure>
      </div>

      <p className={`timedemo__verdict${blocked ? ' is-blocked' : ''}`} role="status">
        {blocked
          ? `Refusé : Moussa passerait à ${formatMinutes(moussaAfter)}, sous le découvert de −5 h. Il doit d’abord rendre service à quelqu’un.`
          : `Échange possible. Quand les deux confirment, ${formatMinutes(minutes)} passent de Moussa à Awa. Personne ne paie, personne n’encaisse.`}
      </p>
    </div>
  )
}
