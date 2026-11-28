import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { listingsApi } from '../../api/endpoints'
import type { Paged, ListingSummary } from '../../api/types'
import { ListingCard } from '../../components/ListingCard'
import { Motif, MotifBand, MotifFabric } from '../../components/Motif'
import { pickHeroListings } from '../../lib/heroListings'
import { directionLabel, typeLabel } from '../../lib/labels'
import { motifFor } from '../../lib/motifs'
import { EASE, gsap, prefersReducedMotion, useGSAP } from '../../motion/gsap'
import { useReveal } from '../../motion/useReveal'
import { plural } from '../../lib/format'
import { SignatureThread } from './SignatureThread'
import { TimeDemo } from './TimeDemo'
import './HomePage.css'

const STEPS = [
  {
    n: '01',
    title: 'Tu proposes',
    text: 'Sur une annonce qui te plaît : un troc contre une de tes annonces, des heures de la banque de temps, ou tu acceptes un don. Un mot pour te présenter.',
  },
  {
    n: '02',
    title: 'L’autre accepte',
    text: 'Il a sept jours pour répondre. S’il accepte, les annonces passent en « réservée » : personne d’autre ne peut les demander.',
  },
  {
    n: '03',
    title: 'Vous vous retrouvez',
    text: 'Une conversation privée s’ouvre, visible par vous deux seulement. Vous choisissez le lieu, à Badalabougou ou à Hamdallaye, et l’heure.',
  },
  {
    n: '04',
    title: 'Vous confirmez tous les deux',
    text: 'L’échange n’est terminé que quand les deux ont cliqué « c’est fait ». Les heures bougent à ce moment-là, et chacun peut laisser un avis.',
  },
] as const

/** Page d'accueil : le récit de TradX, du principe à la première annonce. */
export function HomePage() {
  const root = useRef<HTMLDivElement>(null)
  const hero = useRef<HTMLElement>(null)
  const steps = useRef<HTMLElement>(null)
  const [latest, setLatest] = useState<Paged<ListingSummary> | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    listingsApi
      .search({ pageSize: 7 }, controller.signal)
      .then(setLatest)
      .catch(() => setLatest(null))
    return () => controller.abort()
  }, [])

  useReveal(root, [latest])

  // Entrée du héros : le label, puis chaque ligne du titre sort de son masque, puis les deux fils se croisent.
  useGSAP(
    () => {
      if (prefersReducedMotion()) return
      const tl = gsap.timeline({ defaults: { ease: EASE.out } })
      tl.from('.hero__label', { autoAlpha: 0, y: 10, duration: 0.6 })
        .from('.hero__line > span', { yPercent: 110, duration: 1.1, stagger: 0.11 }, 0.1)
        .from('.hero__lede, .hero__ctas', { autoAlpha: 0, y: 18, duration: 0.8, stagger: 0.12 }, 0.55)
        .from('.hero__weave path', { drawSVG: '0%', duration: 1.6, stagger: 0.18, ease: 'power2.inOut' }, 0.3)
        .from('.hero__weave .hero__node', { scale: 0, transformOrigin: '50% 50%', duration: 0.6, ease: EASE.snap }, 1.45)
    },
    { scope: hero },
  )

  // Les cartes d'annonces arrivent avec les données : elles ont leur propre entrée, une fois chargées.
  const heroListings = pickHeroListings(latest?.items ?? [])
  const heroKey = heroListings.map((l) => l.id).join()
  useGSAP(
    () => {
      if (!heroKey || prefersReducedMotion()) return
      gsap.from('.hero__card', { autoAlpha: 0, y: 26, rotate: (i) => (i ? 4 : -3), duration: 0.9, stagger: 0.14, delay: 0.6 })
    },
    { scope: hero, dependencies: [heroKey] },
  )

  // Les quatre étapes défilent à l'horizontale sur grand écran (section épinglée) ; empilées sur mobile.
  useGSAP(
    () => {
      const mm = gsap.matchMedia()
      mm.add('(min-width: 960px) and (prefers-reduced-motion: no-preference)', () => {
        const track = steps.current?.querySelector<HTMLElement>('.steps__track')
        if (!track) return
        const distance = () => track.scrollWidth - window.innerWidth + 80
        const horizontal = gsap.to(track, {
          x: () => -distance(),
          ease: 'none',
          scrollTrigger: {
            trigger: steps.current,
            pin: true,
            start: 'top top',
            end: () => `+=${distance()}`,
            scrub: 0.6,
            invalidateOnRefresh: true,
          },
        })
        // Parallaxe : le grand numéro glisse moins vite que sa carte, ce qui donne de la profondeur.
        gsap.utils.toArray<HTMLElement>('.step', steps.current).forEach((step) => {
          gsap.fromTo(
            step.querySelector('.step__n'),
            { xPercent: 35 },
            {
              xPercent: -35,
              ease: 'none',
              scrollTrigger: { trigger: step, containerAnimation: horizontal, start: 'left right', end: 'right left', scrub: true },
            },
          )
        })
      })
      return () => mm.revert()
    },
    { scope: steps },
  )

  const items = latest?.items ?? []
  const [feature, ...rest] = items

  return (
    <div ref={root} className="home">
      {/* ---------- Héros ---------- */}
      <section ref={hero} className="hero wrap" aria-labelledby="hero-title">
        <p className="hero__label label">
          <span>TradX · Bamako</span>
          <span className="hero__live">
            <i aria-hidden="true" />
            {latest ? `${plural(latest.totalCount, 'annonce ouverte', 'annonces ouvertes')}` : 'Annonces ouvertes'}
          </span>
        </p>
        <h1 id="hero-title" className="display hero__title">
          <span className="hero__line">
            <span>Échange ce</span>
          </span>
          <span className="hero__line">
            <span>que tu as</span>
          </span>
          <span className="hero__line">
            <span>
              <em>contre ce qu’il</em>
            </span>
          </span>
          <span className="hero__line">
            <span>
              <em>te faut.</em>
            </span>
          </span>
        </h1>

        <svg className="hero__weave" viewBox="0 0 600 420" aria-hidden="true" focusable="false">
          <path d="M-20 70 C 160 60, 230 350, 330 210 S 520 40, 640 130" />
          <path d="M-20 360 C 140 380, 240 60, 330 210 S 510 400, 640 300" />
          <g className="hero__node" transform="translate(330 210)" data-thread-start>
            <circle r="17" />
            <circle r="6" />
          </g>
        </svg>

        {/* Deux vraies annonces de la base (avec leur photo), reliées par le nœud : l'échange en image. */}
        {heroListings.length > 0 && (
          <ul className="hero__cards" aria-label="Annonces du moment">
            {heroListings.map((listing, index) => (
              <li key={listing.id} className={`hero__card hero__card--${index === 0 ? 'a' : 'b'} squircle`}>
                <Link to={`/annonces/${listing.id}`} className="hero__card-link">
                  <span className={`hero__card-media${listing.direction === 'Request' ? ' is-request' : ''}`}>
                    {listing.coverPhotoUrl ? (
                      <img src={listing.coverPhotoUrl} alt="" loading="eager" decoding="async" />
                    ) : (
                      <MotifFabric name={motifFor(listing.categoryName)} cell={22} />
                    )}
                  </span>
                  <span className="label">
                    {directionLabel[listing.direction]} · {typeLabel[listing.type]}
                  </span>
                  <strong>{listing.title}</strong>
                  <span className="muted">{listing.neighborhood ?? listing.city}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}

        <div className="hero__bottom">
          <p className="hero__lede lede">
            Une perceuse contre un cours de maths. Deux heures de couture contre deux heures de jardin. Entre voisins de Bamako, sans un franc.
          </p>
          <div className="hero__ctas">
            <Link to="/annonces" className="btn btn--action">
              Voir les annonces <span className="arrow" aria-hidden="true">→</span>
            </Link>
            <Link to="/publier" className="btn">
              Publier une annonce
            </Link>
          </div>
        </div>
      </section>

      {/* ---------- Le récit, traversé par le fil ---------- */}
      <div className="story">
        <SignatureThread />

        <section className="ways wrap" aria-labelledby="ways-title">
          <header className="ways__head">
            <p className="label" data-reveal>
              Trois façons d’échanger
            </p>
            <h2 id="ways-title" className="h1" data-reveal>
              Ici, rien <em>ne s’achète.</em>
            </h2>
          </header>

          <article className="way way--barter" data-reveal>
            <span className="way__knot" data-knot aria-hidden="true" />
            <Motif name="losange" size={40} className="way__motif" />
            <p className="label">01 · Le troc</p>
            <h3 className="h2">Un objet contre un objet, ou contre un service.</h3>
            <p className="muted">
              Ton vélo d’enfant contre une machine à coudre. Ta vieille télé contre une révision de moto. Chacun propose une de ses annonces, et on s’entend.
            </p>
          </article>

          <article className="way way--time" data-reveal>
            <span className="way__knot" data-knot aria-hidden="true" />
            <Motif name="echelle" size={40} className="way__motif" />
            <p className="label">02 · La banque de temps</p>
            <h3 className="h2">Une heure vaut une heure.</h3>
            <p className="muted">
              Tu répares un frigo pendant deux heures : tu gagnes deux heures, à dépenser chez n’importe quel membre. La professeure et le plombier ont le même compteur.
            </p>
          </article>

          <article className="way way--gift" data-reveal>
            <span className="way__knot" data-knot aria-hidden="true" />
            <Motif name="soleil" size={40} className="way__motif" />
            <p className="label">03 · Le don</p>
            <h3 className="h2">Ou tu donnes, simplement.</h3>
            <p className="muted">Des habits trop petits, des livres de l’an dernier. Ils trouvent quelqu’un au lieu de dormir dans un carton.</p>
          </article>
        </section>

        {/* ---------- Banque de temps, interactive ---------- */}
        <section className="timebank on-ink" aria-labelledby="time-title">
          <MotifBand name="echelle" height={20} className="timebank__band" />
          <div className="wrap timebank__grid">
            <div className="timebank__intro">
              <span className="timebank__knot" data-knot aria-hidden="true" />
              <p className="label" data-reveal>
                La banque de temps, en vrai
              </p>
              <h2 id="time-title" className="h1" data-reveal>
                Chaque maille, <em>trente minutes.</em>
              </h2>
              <p className="lede" data-reveal>
                Tout le monde commence à zéro. On peut devoir jusqu’à cinq heures pour démarrer, pas plus. Au-delà de cent heures, on arrête d’accumuler : le temps est fait pour circuler.
              </p>
            </div>
            <div data-reveal>
              <TimeDemo />
            </div>
          </div>
        </section>

        {/* ---------- Comment se passe un échange ---------- */}
        <section ref={steps} className="steps" aria-labelledby="steps-title">
          <div className="steps__track">
            <header className="steps__head">
              <span className="steps__knot" data-knot aria-hidden="true" />
              <p className="label">Un échange, du début à la fin</p>
              <h2 id="steps-title" className="h1">
                Quatre étapes, <em>deux accords.</em>
              </h2>
            </header>
            <ol className="steps__list">
              {STEPS.map((step) => (
                <li key={step.n} className="step squircle">
                  <span className="step__n" aria-hidden="true">
                    {step.n}
                  </span>
                  <h3 className="h3">{step.title}</h3>
                  <p className="muted">{step.text}</p>
                </li>
              ))}
            </ol>
          </div>
        </section>
      </div>

      {/* ---------- En ce moment à Bamako (données réelles) ---------- */}
      <section className="latest wrap" aria-labelledby="latest-title">
        <header className="latest__head">
          <div>
            <p className="label" data-reveal>
              En ce moment
            </p>
            <h2 id="latest-title" className="h1" data-reveal>
              Ce qui circule <em>à Bamako.</em>
            </h2>
          </div>
          <Link to="/annonces" className="btn" data-reveal>
            Toutes les annonces <span className="arrow" aria-hidden="true">→</span>
          </Link>
        </header>

        {items.length === 0 ? (
          <div className="latest__empty squircle" data-reveal>
            <Motif name="spirale" size={48} />
            <div>
              <h3 className="h3">Le fil commence avec toi.</h3>
              <p className="muted">Aucune annonce publiée pour l’instant. Propose un objet, un service ou un coup de main : la première annonce donne envie aux suivantes.</p>
            </div>
            <Link to="/publier" className="btn btn--action">
              Publier la première
            </Link>
          </div>
        ) : (
          <div className="latest__grid">
            {feature && (
              <div className="latest__feature" data-reveal>
                <ListingCard listing={feature} variant="feature" />
              </div>
            )}
            <div className="latest__side">
              {rest.slice(0, 2).map((listing) => (
                <div key={listing.id} data-reveal>
                  <ListingCard listing={listing} />
                </div>
              ))}
            </div>
            {rest.length > 2 && (
              <div className="latest__rows" data-reveal>
                {rest.slice(2).map((listing) => (
                  <ListingCard key={listing.id} listing={listing} variant="row" />
                ))}
              </div>
            )}
          </div>
        )}
      </section>

      {/* ---------- Confiance ---------- */}
      <section className="trust on-ink" aria-labelledby="trust-title">
        <div className="wrap">
          <p className="label" data-reveal>
            Ce qui protège tout le monde
          </p>
          <h2 id="trust-title" className="h1 trust__title" data-reveal>
            Des règles simples, <em>appliquées par le système.</em>
          </h2>
          <dl className="trust__grid">
            <div data-reveal>
              <dt className="trust__figure">0 F</dt>
              <dd>Aucun prix, aucun paiement. Une annonce qui demande de l’argent peut être signalée comme arnaque.</dd>
            </div>
            <div data-reveal>
              <dt className="trust__figure">2</dt>
              <dd>confirmations pour clore un échange : la tienne et celle de l’autre. Personne ne décide seul.</dd>
            </div>
            <div data-reveal>
              <dt className="trust__figure">3</dt>
              <dd>signalements de membres vérifiés suspendent une annonce, le temps qu’un modérateur regarde. L’auteur peut s’expliquer.</dd>
            </div>
            <div data-reveal>
              <dt className="trust__figure">1</dt>
              <dd>avis par personne et par échange terminé. Pas d’avis sans échange réel.</dd>
            </div>
          </dl>
        </div>
      </section>

      {/* ---------- Appel final ---------- */}
      <section className="final wrap" aria-labelledby="final-title">
        <h2 id="final-title" className="display final__title" data-reveal>
          Qu’as-tu <em>à échanger ?</em>
        </h2>
        <div className="final__actions" data-reveal>
          <Link to="/inscription" className="btn btn--action">
            Créer mon compte <span className="arrow" aria-hidden="true">→</span>
          </Link>
          <Link to="/annonces" className="btn btn--ghost">
            D’abord regarder
          </Link>
        </div>
      </section>
    </div>
  )
}
