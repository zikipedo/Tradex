import './Loader.css'

/** Indicateur de chargement : trois points cousus qui se tissent. Annoncé aux lecteurs d'écran. */
export function Loader({ label = 'Chargement' }: { label?: string }) {
  return (
    <div className="loader" role="status" aria-live="polite">
      <span className="loader__stitches" aria-hidden="true">
        <i />
        <i />
        <i />
      </span>
      <span className="loader__label">{label}…</span>
    </div>
  )
}

/** Message d'erreur réseau ou API, avec une action pour réessayer. */
export function ErrorNote({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="alert" role="alert">
      <p>{message}</p>
      {onRetry && (
        <button type="button" className="btn btn--small" style={{ marginTop: 'var(--s3)' }} onClick={onRetry}>
          Réessayer
        </button>
      )}
    </div>
  )
}
