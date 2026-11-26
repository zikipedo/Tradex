import { useCallback, useEffect, useId, useRef } from 'react'
import { gsap, prefersReducedMotion, ScrollTrigger, useGSAP } from '../../motion/gsap'

/**
 * Le fil d'échange : idée signature de TradX.
 * Un fil ocre traverse la section parente et passe par chaque point marqué `[data-knot]` (les étapes du récit).
 * Il se dessine au rythme du défilement ; un nœud le suit et passe sur chaque étape.
 *
 * Le tracé est calculé à partir des positions réelles des points d'ancrage : il s'adapte à toutes
 * les largeurs d'écran et se recalcule quand la mise en page change.
 * Le composant doit être l'enfant direct de l'élément qu'il traverse (positionné en `relative`).
 */
export function SignatureThread() {
  const svgRef = useRef<SVGSVGElement>(null)
  const pathRef = useRef<SVGPathElement>(null)
  const knotRef = useRef<SVGGElement>(null)
  const pathId = `fil-${useId().replace(/:/g, '')}`

  /** Construit une courbe douce qui ondule entre les ancres. Renvoie faux si le tracé est impossible. */
  const buildPath = useCallback((): boolean => {
    const svg = svgRef.current
    const path = pathRef.current
    const root = svg?.parentElement
    if (!svg || !path || !root) return false
    const box = root.getBoundingClientRect()
    if (box.width === 0 || box.height === 0) return false
    svg.setAttribute('viewBox', `0 0 ${box.width} ${box.height}`)
    const anchors = Array.from(root.querySelectorAll<HTMLElement>('[data-knot]')).map((el) => {
      const r = el.getBoundingClientRect()
      return { x: r.left - box.left + r.width / 2, y: r.top - box.top + r.height / 2 }
    })
    if (anchors.length < 2) return false
    // Le fil part du nœud du héros (là où les deux fils se croisent) s'il existe, sinon du coin haut droit.
    const origin = document.querySelector('[data-thread-start]')?.getBoundingClientRect()
    const start = origin
      ? { x: origin.left - box.left + origin.width / 2, y: origin.top - box.top + origin.height / 2 }
      : { x: box.width * 0.92, y: 0 }
    const points = [start, ...anchors]
    const f = (n: number) => n.toFixed(1)
    let d = `M ${f(start.x)} ${f(start.y)}`
    for (let i = 1; i < points.length; i++) {
      const a = points[i - 1]!
      const b = points[i]!
      const dy = b.y - a.y
      if (i === 1) {
        // Premier tronçon : descend d'abord, puis arrive à l'horizontale sur la première étape,
        // en suivant son filet supérieur, sans traverser le titre de la section.
        d += ` C ${f(a.x)} ${f(a.y + dy * 0.85)}, ${f(b.x + Math.min(box.width * 0.5, 620))} ${f(b.y)}, ${f(b.x)} ${f(b.y)}`
        continue
      }
      // Les poignées s'écartent latéralement : le fil « tisse » au lieu de descendre tout droit.
      const swing = (i % 2 === 0 ? 1 : -1) * Math.min(box.width * 0.28, 260)
      d += ` C ${f(a.x + swing)} ${f(a.y + dy * 0.45)}, ${f(b.x - swing)} ${f(b.y - dy * 0.45)}, ${f(b.x)} ${f(b.y)}`
    }
    path.setAttribute('d', d)
    return true
  }, [])

  // Recalcul quand la section change de taille (images chargées, polices, rotation du téléphone).
  useEffect(() => {
    const root = svgRef.current?.parentElement
    if (!root) return
    let frame = 0
    const observer = new ResizeObserver(() => {
      cancelAnimationFrame(frame)
      frame = requestAnimationFrame(() => {
        if (buildPath()) ScrollTrigger.refresh()
      })
    })
    observer.observe(root)
    return () => {
      cancelAnimationFrame(frame)
      observer.disconnect()
    }
  }, [buildPath])

  useGSAP(
    () => {
      const path = pathRef.current
      const knot = knotRef.current
      if (!buildPath() || !path || !knot) return

      if (prefersReducedMotion()) {
        gsap.set(knot, { autoAlpha: 0 })
        return
      }

      gsap.set(path, { drawSVG: '0%' })
      const tl = gsap.timeline({
        defaults: { ease: 'none' },
        scrollTrigger: {
          trigger: svgRef.current!.parentElement,
          start: 'top 70%',
          end: 'bottom 60%',
          scrub: 0.8,
          invalidateOnRefresh: true,
        },
      })
      // Le nœud est dessiné autour de (0,0) dans le même repère SVG que le fil : pas besoin d'alignement.
      tl.to(path, { drawSVG: '100%' }, 0).to(knot, { motionPath: { path, autoRotate: false } }, 0)
    },
    { scope: svgRef },
  )

  return (
    <svg ref={svgRef} className="signature-thread" aria-hidden="true" focusable="false" preserveAspectRatio="none">
      {/* Le tracé à venir, en pointillés discrets : on voit où le fil va passer. */}
      <use href={`#${pathId}`} className="signature-thread__guide" />
      <path ref={pathRef} id={pathId} className="signature-thread__line" />
      <g ref={knotRef} className="signature-thread__knot">
        <circle r="11" />
        <circle r="4.5" />
      </g>
    </svg>
  )
}
