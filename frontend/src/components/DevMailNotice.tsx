/**
 * Rappel affiché **en développement seulement** : l'API n'envoie pas de vrais e-mails, elle les écrit
 * dans un dossier local. Sans ce rappel, on attend un e-mail qui n'arrivera jamais.
 * En production (`npm run build`), `import.meta.env.DEV` vaut false : le composant ne rend rien
 * et son texte est retiré du code livré.
 */
export function DevMailNotice() {
  if (!import.meta.env.DEV) return null
  return (
    <div className="dev-notice" role="note" aria-label="Rappel pour le développement">
      <p className="label">Mode développement</p>
      <p>
        Aucun e-mail n’est vraiment envoyé. Ouvre le fichier le plus récent du dossier <code>src/TradX.Api/.mail-outbox</code>, puis
        copie dans le navigateur le lien qu’il contient.
      </p>
    </div>
  )
}
