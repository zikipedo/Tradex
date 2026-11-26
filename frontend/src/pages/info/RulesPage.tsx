/** Règles de la communauté : ce qui est interdit, comment fonctionnent les signalements et la modération. */
export default function RulesPage() {
  return (
    <div className="wrap page">
      <header className="page-head">
        <p className="label">Règles de la communauté</p>
        <h1 className="h1">
          Ce qui garde TradX <em>sain.</em>
        </h1>
      </header>
      <article className="prose">
        <h2 className="h2">Pas d’argent, jamais</h2>
        <p>Aucune annonce ne doit demander de l’argent, ni directement ni à demi-mot. C’est un motif de signalement (« arnaque ou demande d’argent »).</p>

        <h2 className="h2">Ce qui est interdit</h2>
        <ul>
          <li>Les armes, les médicaments, les animaux.</li>
          <li>Tout objet ou service illégal.</li>
          <li>Les contenus insultants, discriminants ou trompeurs.</li>
        </ul>

        <h2 className="h2">Signalements et suspension</h2>
        <p>
          Chaque membre peut signaler une annonce. Quand trois membres différents, à l’e-mail confirmé et inscrits depuis au moins sept jours, signalent la même
          annonce, elle est suspendue par précaution. Son auteur peut s’expliquer, puis un modérateur décide de la rétablir ou de la laisser suspendue.
        </p>

        <h2 className="h2">Avis</h2>
        <p>Un avis ne peut être laissé qu’après un échange terminé, un seul par personne et par échange. Pas d’avis sans échange réel.</p>

        <h2 className="h2">Tes données</h2>
        <p>
          Ton e-mail n’est jamais affiché. Ton profil public montre ton nom affiché, ta ville, ton quartier, ta présentation et tes avis. Les conversations ne
          sont visibles que par les deux personnes de l’échange, et par un modérateur uniquement en cas de litige.
        </p>
      </article>
    </div>
  )
}
