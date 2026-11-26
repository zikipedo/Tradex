import { useRef } from 'react'
import type { ExchangeStatus } from '../../api/types'
import { EASE, gsap, prefersReducedMotion, useGSAP } from '../../motion/gsap'

/**
 * L'état d'un échange dessiné avec le fil signature :
 * - proposé : deux fils qui s'approchent sans se toucher ;
 * - accepté : les fils se croisent ;
 * - terminé : ils se nouent ;
 * - refusé, annulé, expiré : un fil se détend et retombe ;
 * - litige : le nœud est marqué en rouille.
 * Le texte de l'état est toujours affiché à côté : le dessin n'est pas la seule source d'information.
 */
export function ExchangeKnot({ status, leftName, rightName }: { status: ExchangeStatus; leftName: string; rightName: string }) {
  const svg = useRef<SVGSVGElement>(null)
  const crossed = status === 'Accepted' || status === 'Completed' || status === 'Disputed'
  const broken = status === 'Declined' || status === 'Cancelled' || status === 'Expired'

  // Trajectoires : séparées (proposé), croisées (accepté et suivants), retombées (fermé sans accord).
  const top = crossed ? 'M 20 40 C 150 40, 210 120, 300 80 S 450 120, 580 120' : broken ? 'M 20 40 C 140 40, 200 60, 260 150' : 'M 20 40 C 150 40, 210 60, 280 70'
  const bottom = crossed ? 'M 20 120 C 150 120, 210 40, 300 80 S 450 40, 580 40' : 'M 580 120 C 450 120, 390 100, 320 90'

  useGSAP(
    () => {
      if (prefersReducedMotion()) return
      gsap.from('path', { drawSVG: '0%', duration: 1.2, ease: 'power2.inOut', stagger: 0.12 })
      gsap.from('.knot', { scale: 0, transformOrigin: '50% 50%', duration: 0.6, ease: EASE.snap, delay: 0.9 })
    },
    { scope: svg, dependencies: [status], revertOnUpdate: true },
  )

  return (
    <figure className={`xknot xknot--${status.toLowerCase()}`}>
      <svg ref={svg} viewBox="0 0 600 160" aria-hidden="true" focusable="false">
        <path d={top} className="xknot__a" />
        <path d={bottom} className="xknot__b" />
        {crossed && (
          <g className="knot" transform="translate(300 80)">
            <circle r={status === 'Completed' ? 16 : 11} />
            <circle r={status === 'Completed' ? 7 : 4} />
          </g>
        )}
      </svg>
      <figcaption className="xknot__names">
        <span>{leftName}</span>
        <span>{rightName}</span>
      </figcaption>
    </figure>
  )
}
