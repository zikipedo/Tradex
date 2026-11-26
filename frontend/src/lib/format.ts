/** Formatage des durées et des dates pour l'affichage (français, fuseau de Bamako = UTC). */

const TIME_ZONE = 'Africa/Bamako'

/**
 * Durée lisible à partir de minutes entières : 90 → « 1 h 30 », 30 → « 30 min », -300 → « −5 h ».
 * Le signe moins typographique (−) est utilisé pour les soldes négatifs.
 */
export function formatMinutes(minutes: number, options: { signed?: boolean } = {}): string {
  const sign = minutes < 0 ? '−' : options.signed && minutes > 0 ? '+' : ''
  const abs = Math.abs(Math.trunc(minutes))
  const h = Math.floor(abs / 60)
  const m = abs % 60
  if (h === 0) return `${sign}${m} min`
  return m === 0 ? `${sign}${h} h` : `${sign}${h} h ${String(m).padStart(2, '0')}`
}

/** Date courte : « 26 nov. 2026 ». */
export function formatDate(iso: string): string {
  return new Intl.DateTimeFormat('fr-FR', { day: 'numeric', month: 'short', year: 'numeric', timeZone: TIME_ZONE }).format(
    new Date(iso),
  )
}

/** Heure d'un message : « 14:05 ». */
export function formatTime(iso: string): string {
  return new Intl.DateTimeFormat('fr-FR', { hour: '2-digit', minute: '2-digit', timeZone: TIME_ZONE }).format(new Date(iso))
}

/** Date relative courte : « à l'instant », « il y a 3 h », « il y a 2 j », puis la date. */
export function formatRelative(iso: string, now: Date = new Date()): string {
  const seconds = Math.round((now.getTime() - new Date(iso).getTime()) / 1000)
  if (seconds < 60) return 'à l’instant'
  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `il y a ${minutes} min`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `il y a ${hours} h`
  const days = Math.round(hours / 24)
  if (days < 7) return `il y a ${days} j`
  return formatDate(iso)
}

/** Initiales d'un nom affiché : « Awa Traoré » → « AT ». */
export function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('')
}

/** Nombre avec pluriel simple : (3, 'annonce') → « 3 annonces ». */
export function plural(count: number, word: string, pluralWord = `${word}s`): string {
  return `${new Intl.NumberFormat('fr-FR').format(count)} ${count > 1 ? pluralWord : word}`
}
