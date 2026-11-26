import { Link } from 'react-router-dom'
import { Motif } from '../../components/Motif'

/** Page introuvable : le fil s'est perdu. */
export default function NotFoundPage() {
  return (
    <div className="wrap page" style={{ display: 'grid', gap: 'var(--s5)', justifyItems: 'start', paddingBlock: 'var(--s9)' }}>
      <Motif name="spirale" size={64} />
      <p className="label muted">Erreur 404</p>
      <h1 className="display">
        Le fil <em>s’est perdu.</em>
      </h1>
      <p className="lede">Cette page n’existe pas, ou plus. L’annonce a peut-être été échangée.</p>
      <div className="form-actions">
        <Link to="/annonces" className="btn btn--action">
          Voir les annonces
        </Link>
        <Link to="/" className="btn btn--ghost">
          Accueil
        </Link>
      </div>
    </div>
  )
}
