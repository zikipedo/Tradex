import { useGSAP } from '@gsap/react'
import gsap from 'gsap'
import { DrawSVGPlugin } from 'gsap/DrawSVGPlugin'
import { MotionPathPlugin } from 'gsap/MotionPathPlugin'
import { ScrollTrigger } from 'gsap/ScrollTrigger'

/**
 * Point d'entrée unique de GSAP : les plugins sont enregistrés une seule fois ici.
 * - ScrollTrigger : scènes liées au défilement ;
 * - DrawSVG : le fil signature qui se dessine ;
 * - MotionPath : le nœud qui suit le fil.
 */
gsap.registerPlugin(useGSAP, ScrollTrigger, DrawSVGPlugin, MotionPathPlugin)

/** Courbes de la direction « Bogolan » (mesurées sur les références puis ajustées). */
export const EASE = {
  /** Entrées : décélération longue, sans rebond. Équivaut à cubic-bezier(.2,.8,.2,1). */
  out: 'expo.out',
  /** Petits retours d'interaction : léger dépassement. */
  snap: 'back.out(1.6)',
  /** Mouvements liés au défilement : linéaire, c'est le défilement qui donne le rythme. */
  scroll: 'none',
} as const

/** Durées en secondes, alignées sur les jetons CSS. */
export const DURATION = { fast: 0.16, base: 0.32, slow: 0.7 } as const

gsap.defaults({ ease: EASE.out, duration: DURATION.slow })

/** Vrai si l'utilisateur a demandé moins d'animations. */
export function prefersReducedMotion(): boolean {
  return typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

export { gsap, ScrollTrigger, useGSAP }
