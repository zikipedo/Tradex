/** Noms des motifs bogolan dessinés par le composant `Motif`. */
export type MotifName =
  | 'zigzag'
  | 'points'
  | 'croix'
  | 'losange'
  | 'peigne'
  | 'spirale'
  | 'echelle'
  | 'soleil'
  | 'damier'
  | 'vague'
  | 'triangles'
  | 'cercles'

export const MOTIFS: MotifName[] = [
  'zigzag',
  'points',
  'croix',
  'losange',
  'peigne',
  'spirale',
  'echelle',
  'soleil',
  'damier',
  'vague',
  'triangles',
  'cercles',
]

/**
 * Un motif choisi pour chaque catégorie initiale (sens visuel : la couture en zigzag, les cours en échelle…).
 * Les clés sont normalisées (minuscules, sans accents) pour résister aux petites variations de libellé.
 */
const BY_CATEGORY: Record<string, MotifName> = {
  'vetements et mode': 'zigzag',
  'cours et soutien scolaire': 'echelle',
  'cuisine et alimentation': 'points',
  'bricolage et reparation': 'croix',
  'maison et jardin': 'losange',
  'enfants et bebe': 'spirale',
  'beaute et bien-etre': 'soleil',
  informatique: 'peigne',
  electronique: 'damier',
  'transport et demenagement': 'vague',
  'sport et loisirs': 'triangles',
  autres: 'cercles',
}

function normalize(value: string): string {
  return value.normalize('NFD').replace(/\p{Diacritic}/gu, '').trim().toLowerCase()
}

/**
 * Motif d'une catégorie : celui choisi pour les catégories initiales, sinon un motif stable
 * calculé par hachage FNV-1a 32 bits (même catégorie, même motif, à chaque affichage).
 */
export function motifFor(category: string): MotifName {
  const chosen = BY_CATEGORY[normalize(category)]
  if (chosen) return chosen
  let hash = 0x811c9dc5
  for (const char of category) {
    hash ^= char.codePointAt(0)!
    hash = Math.imul(hash, 0x01000193) >>> 0
  }
  return MOTIFS[hash % MOTIFS.length]!
}
