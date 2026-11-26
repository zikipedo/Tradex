import Lenis from 'lenis'
import 'lenis/dist/lenis.css'
import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { gsap, prefersReducedMotion, ScrollTrigger } from './gsap'

/**
 * Défilement amorti (Lenis) synchronisé avec ScrollTrigger sur l'horloge de GSAP.
 * Désactivé si l'utilisateur préfère moins d'animations : le défilement natif est alors conservé.
 * Remonte en haut de page à chaque changement de route.
 */
export function SmoothScroll() {
  const { pathname } = useLocation()

  useEffect(() => {
    if (prefersReducedMotion()) return
    const lenis = new Lenis({ duration: 1.05, smoothWheel: true, wheelMultiplier: 0.95 })
    lenis.on('scroll', ScrollTrigger.update)
    const tick = (time: number) => lenis.raf(time * 1000)
    gsap.ticker.add(tick)
    gsap.ticker.lagSmoothing(0)
    ;(window as Window & { __lenis?: Lenis }).__lenis = lenis
    return () => {
      gsap.ticker.remove(tick)
      lenis.destroy()
      delete (window as Window & { __lenis?: Lenis }).__lenis
    }
  }, [])

  useEffect(() => {
    const lenis = (window as Window & { __lenis?: Lenis }).__lenis
    if (lenis) lenis.scrollTo(0, { immediate: true })
    else window.scrollTo(0, 0)
    // Les hauteurs changent avec la nouvelle page : on recalcule les déclencheurs.
    requestAnimationFrame(() => ScrollTrigger.refresh())
  }, [pathname])

  return null
}
