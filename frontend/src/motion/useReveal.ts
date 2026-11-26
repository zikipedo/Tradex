import type { RefObject } from 'react'
import { DURATION, EASE, gsap, prefersReducedMotion, ScrollTrigger, useGSAP } from './gsap'

/**
 * Révèle les éléments marqués `[data-reveal]` quand ils entrent dans l'écran :
 * montée de 28 px + opacité, 700 ms, décalage de 110 ms entre éléments arrivés ensemble.
 * Les éléments déjà visibles au chargement sont révélés immédiatement (pas de page vide).
 * Sans animation si l'utilisateur l'a demandé.
 */
export function useReveal(scope: RefObject<HTMLElement | null>, deps: unknown[] = []) {
  useGSAP(
    () => {
      const items = gsap.utils.toArray<HTMLElement>('[data-reveal]')
      if (items.length === 0) return
      if (prefersReducedMotion()) {
        gsap.set(items, { autoAlpha: 1, y: 0 })
        return
      }
      // Les éléments déjà dans l'écran sont révélés tout de suite : ScrollTrigger ne les évaluerait
      // qu'au premier défilement, et une page qu'on ne fait pas défiler resterait vide.
      const threshold = window.innerHeight * 0.88
      const inView = items.filter((el) => el.getBoundingClientRect().top < threshold)
      const below = items.filter((el) => !inView.includes(el))
      gsap.fromTo(
        inView,
        { autoAlpha: 0, y: 28 },
        { autoAlpha: 1, y: 0, duration: DURATION.slow, ease: EASE.out, stagger: 0.09, delay: 0.05 },
      )
      if (below.length === 0) return
      gsap.set(below, { autoAlpha: 0, y: 28 })
      ScrollTrigger.batch(below, {
        start: 'top 88%',
        once: true,
        onEnter: (batch) =>
          gsap.to(batch, { autoAlpha: 1, y: 0, duration: DURATION.slow, ease: EASE.out, stagger: 0.11, overwrite: true }),
      })
    },
    { scope, dependencies: deps, revertOnUpdate: true },
  )
}
