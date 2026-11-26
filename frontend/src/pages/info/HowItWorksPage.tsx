import { Link } from 'react-router-dom'

/** Explication détaillée du fonctionnement : les trois façons d'échanger et la banque de temps. */
export default function HowItWorksPage() {
  return (
    <div className="wrap page">
      <header className="page-head">
        <p className="label">Comment ça marche</p>
        <h1 className="h1">
          Échanger sans argent, <em>concrètement.</em>
        </h1>
      </header>
      <article className="prose">
        <p className="lede">
          TradX met en relation des voisins qui ont quelque chose à offrir et d’autres qui en ont besoin. Rien ne s’y achète : on troque, on s’échange du temps, ou on donne.
        </p>

        <h2 className="h2">1. Publier une annonce</h2>
        <p>
          Dis si tu <strong>proposes</strong> ou si tu <strong>cherches</strong>, s’il s’agit d’un objet ou d’un service, et ce qui t’intéresserait en retour. Ajoute
          jusqu’à cinq photos : elles sont réduites et nettoyées automatiquement (la position GPS est retirée). Ton e-mail doit être confirmé pour publier.
        </p>

        <h2 className="h2">2. Proposer un échange</h2>
        <ul>
          <li>
            <strong>Troc</strong> : tu proposes une de tes annonces publiées en retour.
          </li>
          <li>
            <strong>Banque de temps</strong> (services uniquement) : tu indiques une durée, de 30 minutes à 8 heures, par tranches de 30 minutes.
          </li>
          <li>
            <strong>Don</strong> : si l’annonce est un don, tu la demandes sans contrepartie.
          </li>
        </ul>

        <h2 className="h2">3. Se mettre d’accord</h2>
        <p>
          L’auteur a sept jours pour accepter ou refuser. Une fois accepté, les objets concernés sont réservés et une conversation privée s’ouvre entre vous deux.
          Retrouvez-vous de préférence dans un lieu public.
        </p>

        <h2 className="h2">4. Confirmer, tous les deux</h2>
        <p>
          Quand l’échange a eu lieu, chacun clique sur « C’est fait ». L’échange se termine à la deuxième confirmation : c’est seulement là que les heures passent
          d’un compte à l’autre. Chacun peut ensuite laisser un avis. En cas de problème, un litige peut être ouvert : un modérateur tranche.
        </p>

        <h2 className="h2">La banque de temps</h2>
        <ul>
          <li>Une heure de service vaut une heure, quel que soit le service.</li>
          <li>Tout le monde commence à zéro. Le découvert est permis jusqu’à −5 heures, pour pouvoir démarrer.</li>
          <li>Le solde est plafonné à +100 heures : le temps est fait pour circuler.</li>
          <li>Les heures ne s’achètent pas, ne se vendent pas et ne se retirent pas.</li>
        </ul>

        <div className="form-actions" style={{ marginTop: 'var(--s5)' }}>
          <Link to="/annonces" className="btn btn--action">
            Voir les annonces
          </Link>
          <Link to="/regles" className="btn">
            Les règles de la communauté
          </Link>
        </div>
      </article>
    </div>
  )
}
