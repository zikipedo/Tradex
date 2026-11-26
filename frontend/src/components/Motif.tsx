/**
 * Motifs géométriques inspirés des tissus bogolan, dessinés en SVG (aucune image, aucune copie de pièce existante).
 * Chaque catégorie reçoit un motif stable : il aide à repérer une catégorie d'un coup d'œil dans les listes.
 */

import type { MotifName } from '../lib/motifs'

export type { MotifName }

function paths(name: MotifName) {
  switch (name) {
    case 'zigzag':
      return <polyline points="2,16 6,8 10,16 14,8 18,16 22,8" />
    case 'points':
      return (
        <g fill="currentColor" stroke="none">
          {[6, 12, 18].flatMap((x) => [6, 12, 18].map((y) => <circle key={`${x}-${y}`} cx={x} cy={y} r={1.7} />))}
        </g>
      )
    case 'croix':
      return (
        <>
          <path d="M12 3v18M3 12h18" />
          <g fill="currentColor" stroke="none">
            <circle cx={6.5} cy={6.5} r={1.6} />
            <circle cx={17.5} cy={6.5} r={1.6} />
            <circle cx={6.5} cy={17.5} r={1.6} />
            <circle cx={17.5} cy={17.5} r={1.6} />
          </g>
        </>
      )
    case 'losange':
      return (
        <>
          <path d="M12 2l10 10-10 10L2 12z" />
          <path d="M12 8l4 4-4 4-4-4z" fill="currentColor" />
        </>
      )
    case 'peigne':
      return <path d="M3 7h18M5 7v11M9.7 7v11M14.3 7v11M19 7v11" />
    case 'spirale':
      return <path d="M12 12m0-1.5a1.5 1.5 0 1 1-1.5 1.5c0-2.5 2-4.5 4.5-4.5s4.5 2 4.5 4.5-3 7.5-7.5 7.5S4 16 4 12 7.5 4 12 4" />
    case 'echelle':
      return <path d="M7 2v20M17 2v20M7 6h10M7 12h10M7 18h10" />
    case 'soleil':
      return (
        <>
          <circle cx={12} cy={12} r={4} />
          <path d="M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M4.9 19.1L7 17M17 7l2.1-2.1" />
        </>
      )
    case 'damier':
      return (
        <g fill="currentColor" stroke="none">
          <rect x={3} y={3} width={6} height={6} rx={1} />
          <rect x={15} y={3} width={6} height={6} rx={1} />
          <rect x={9} y={9} width={6} height={6} rx={1} />
          <rect x={3} y={15} width={6} height={6} rx={1} />
          <rect x={15} y={15} width={6} height={6} rx={1} />
        </g>
      )
    case 'vague':
      return <path d="M2 9c2.5-3 5-3 7.5 0s5 3 7.5 0 3.5-2 5 0M2 16c2.5-3 5-3 7.5 0s5 3 7.5 0 3.5-2 5 0" />
    case 'triangles':
      return (
        <>
          <path d="M3 20l5-9 5 9z" />
          <path d="M11 20l5-9 5 9z" fill="currentColor" />
          <path d="M7 9l5-6 5 6" />
        </>
      )
    case 'cercles':
      return (
        <>
          <circle cx={12} cy={12} r={9} />
          <circle cx={12} cy={12} r={5} />
          <circle cx={12} cy={12} r={1.6} fill="currentColor" />
        </>
      )
  }
}

export function Motif({ name, size = 24, className }: { name: MotifName; size?: number; className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      width={size}
      height={size}
      className={className}
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {paths(name)}
    </svg>
  )
}

/**
 * Échantillon de tissu : le motif répété sur toute la surface, en quinconce comme sur un bogolan.
 * Sert d'image aux annonces sans photo ; la couleur vient de `currentColor`.
 */
export function MotifFabric({ name, cell = 34 }: { name: MotifName; cell?: number }) {
  const id = `fabric-${name}-${cell}`
  return (
    <svg className="motif-fabric" width="100%" height="100%" aria-hidden="true" focusable="false">
      <defs>
        <pattern id={id} width={cell * 2} height={cell * 2} patternUnits="userSpaceOnUse">
          <g fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round">
            <g transform={`translate(${cell * 0.18} ${cell * 0.18}) scale(${(cell * 0.64) / 24})`}>{paths(name)}</g>
            <g transform={`translate(${cell * 1.18} ${cell * 1.18}) scale(${(cell * 0.64) / 24})`}>{paths(name)}</g>
          </g>
          <path d={`M0 ${cell} H${cell * 2}`} stroke="currentColor" strokeWidth={0.8} strokeDasharray="3 5" opacity={0.6} />
        </pattern>
      </defs>
      <rect width="100%" height="100%" fill={`url(#${id})`} />
    </svg>
  )
}

/** Bande de motifs répétés (séparateur de section), dessinée par un motif SVG `<pattern>`. */
export function MotifBand({ name, height = 28, className }: { name: MotifName; height?: number; className?: string }) {
  const id = `band-${name}`
  return (
    <svg className={className} width="100%" height={height} aria-hidden="true" focusable="false">
      <defs>
        <pattern id={id} width={height * 1.4} height={height} patternUnits="userSpaceOnUse">
          <g
            transform={`translate(${height * 0.2} 0) scale(${height / 24})`}
            fill="none"
            stroke="currentColor"
            strokeWidth={1.6}
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            {paths(name)}
          </g>
        </pattern>
      </defs>
      <rect width="100%" height="100%" fill={`url(#${id})`} />
    </svg>
  )
}
