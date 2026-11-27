/** Messages de l'envoi de photos, accordés selon le nombre (aucun « 1 premières » ni « suivante(s) »). */

/** Message quand le membre choisit plus de photos qu'il ne reste de places ; null si tout tient. */
export function selectionNotice(chosen: number, remaining: number, max: number): string | null {
  if (chosen <= remaining) return null
  const kept = remaining === 1 ? 'seule la première est ajoutée' : `seules les ${remaining} premières sont ajoutées`
  return `Tu as choisi ${chosen} photos : ${kept} (${max} au maximum par annonce).`
}

/** Message d'échec d'un envoi, avec le nom du fichier et le nombre de photos non envoyées après lui. */
export function uploadFailure(fileName: string, reason: string, notSent: number): string {
  const skipped =
    notSent === 0 ? '' : notSent === 1 ? ' La photo suivante n’a pas été envoyée.' : ` Les ${notSent} photos suivantes n’ont pas été envoyées.`
  return `« ${fileName} » : ${reason}${skipped}`
}
