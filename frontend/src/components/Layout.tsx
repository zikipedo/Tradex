import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { formatMinutes, initials } from '../lib/format'
import { Logo } from './Logo'
import { MotifBand } from './Motif'
import './Layout.css'

/** Gabarit commun : en-tête collant, menu mobile, contenu de la page, pied de page. */
export function Layout() {
  const { status, me, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  // Le menu mémorise la page où il a été ouvert : changer de page le ferme sans effet supplémentaire.
  const [menuPath, setMenuPath] = useState<string | null>(null)
  const menuOpen = menuPath === location.pathname
  const setMenuOpen = (open: boolean | ((was: boolean) => boolean)) => {
    const next = typeof open === 'function' ? open(menuOpen) : open
    setMenuPath(next ? location.pathname : null)
  }
  const [scrolled, setScrolled] = useState(false)

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 12)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  useEffect(() => {
    document.body.style.overflow = menuOpen ? 'hidden' : ''
    if (!menuOpen) return
    const onKey = (event: KeyboardEvent) => event.key === 'Escape' && setMenuPath(null)
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [menuOpen])

  const onLogout = async () => {
    await logout()
    navigate('/')
  }

  const links = (
    <>
      <NavLink to="/annonces">Annonces</NavLink>
      {status === 'member' && <NavLink to="/echanges">Mes échanges</NavLink>}
      {status === 'member' && <NavLink to="/mes-annonces">Mes annonces</NavLink>}
      <NavLink to="/comment-ca-marche">Comment ça marche</NavLink>
    </>
  )

  return (
    <div className="shell">
      <a className="skip-link" href="#contenu">
        Aller au contenu
      </a>
      <header className={`topbar${scrolled ? ' is-scrolled' : ''}`}>
        <div className="wrap topbar__inner">
          <Link to="/" className="topbar__logo" aria-label="TradX, accueil">
            <Logo />
          </Link>
          <nav className="topbar__nav" aria-label="Navigation principale">
            {links}
          </nav>
          <div className="topbar__actions">
            {status === 'member' && me ? (
              <>
                <Link to="/temps" className="time-chip" title="Mon compte de temps">
                  <span className="label">Temps</span>
                  <span className="tabular">{formatMinutes(me.timeBalanceMinutes, { signed: true })}</span>
                </Link>
                <Link to="/publier" className="btn btn--action btn--small topbar__publish">
                  Publier
                </Link>
                <Link to="/moi" className="avatar" aria-label={`Mon profil, ${me.displayName}`}>
                  {initials(me.displayName)}
                </Link>
              </>
            ) : status === 'guest' ? (
              <>
                <Link to="/connexion" className="btn btn--ghost btn--small topbar__login">
                  Se connecter
                </Link>
                <Link to="/inscription" className="btn btn--action btn--small">
                  Créer un compte
                </Link>
              </>
            ) : null}
            <button
              type="button"
              className="topbar__burger"
              aria-expanded={menuOpen}
              aria-controls="menu-mobile"
              onClick={() => setMenuOpen((open) => !open)}
            >
              <span className="sr-only">{menuOpen ? 'Fermer le menu' : 'Ouvrir le menu'}</span>
              <span aria-hidden="true" />
              <span aria-hidden="true" />
            </button>
          </div>
        </div>
      </header>

      <div id="menu-mobile" className={`sheet on-ink${menuOpen ? ' is-open' : ''}`} inert={!menuOpen}>
        <nav className="sheet__nav" aria-label="Menu">
          {links}
          {status === 'member' ? (
            <>
              <NavLink to="/publier">Publier une annonce</NavLink>
              <NavLink to="/temps">Mon temps</NavLink>
              <NavLink to="/moi">Mon profil</NavLink>
              <button type="button" onClick={onLogout}>
                Se déconnecter
              </button>
            </>
          ) : (
            <>
              <NavLink to="/connexion">Se connecter</NavLink>
              <NavLink to="/inscription">Créer un compte</NavLink>
            </>
          )}
        </nav>
      </div>

      <main id="contenu" tabIndex={-1}>
        <Outlet />
      </main>

      <footer className="footer on-ink">
        <MotifBand name="zigzag" height={22} className="footer__band" />
        <div className="wrap footer__grid">
          <div>
            <Logo />
            <p className="footer__pitch">
              Échanger des objets et des services entre voisins, à Bamako. Pas d’argent : du troc, du temps et des dons.
            </p>
          </div>
          <nav aria-label="Liens du pied de page" className="footer__links">
            <Link to="/annonces">Toutes les annonces</Link>
            <Link to="/comment-ca-marche">Comment ça marche</Link>
            <Link to="/regles">Règles de la communauté</Link>
            {status === 'member' && (
              <button type="button" onClick={onLogout}>
                Se déconnecter
              </button>
            )}
          </nav>
          <p className="footer__note label">Bamako · Mali · 0 F CFA échangé, par principe</p>
        </div>
      </footer>
    </div>
  )
}
