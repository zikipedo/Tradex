import { useState } from 'react'
import { Link } from 'react-router-dom'
import { exchangesApi } from '../../api/endpoints'
import { ErrorNote, Loader } from '../../components/Loader'
import { TimeStrip } from '../../components/TimeStrip'
import { formatDate, formatMinutes } from '../../lib/format'
import { useResource } from '../../lib/useAsync'

/** Mon compte de temps : solde, bande tissée, limites et historique des mouvements. */
export default function TimeAccountPage() {
  const [page, setPage] = useState(1)
  const { data, error, loading, reload } = useResource(() => exchangesApi.timeAccount(page), [page])

  if (loading && !data) return <Loader label="Chargement de ton compte de temps" />
  if (error || !data)
    return (
      <div className="wrap page">
        <ErrorNote message={error ?? 'Compte introuvable.'} onRetry={reload} />
      </div>
    )

  const balance = data.balanceMinutes
  const tone = balance > 0 ? 'credit' : balance < 0 ? 'debit' : 'neutral'
  const room = balance - data.overdraftLimitMinutes

  return (
    <div className="wrap page">
      <header className="page-head">
        <p className="label">Mon temps</p>
        <h1 className="display" style={{ fontSize: 'clamp(3.4rem, 10vw, 8rem)' }}>
          <span className="tabular">{formatMinutes(balance, { signed: true })}</span>
        </h1>
        <p className="lede">
          {tone === 'credit' && 'Tu as donné plus de temps que tu n’en as reçu. Ces heures t’attendent chez n’importe quel membre.'}
          {tone === 'debit' && 'Tu as reçu plus que tu n’as donné. Rends service à ton tour pour remonter.'}
          {tone === 'neutral' && 'Tu es à l’équilibre. Ton premier service rendu te donnera tes premières heures.'}
        </p>
      </header>

      <section className="panel" aria-label="Solde en mailles">
        <TimeStrip balanceMinutes={balance} overdraftLimitMinutes={data.overdraftLimitMinutes} maxBalanceMinutes={data.maxBalanceMinutes} visiblePositiveSlots={30} />
        <div className="time-legend">
          <span>Découvert possible : {formatMinutes(data.overdraftLimitMinutes)}</span>
          <span>Tu peux encore recevoir {formatMinutes(Math.max(0, room))} de services</span>
          <span>Plafond : {formatMinutes(data.maxBalanceMinutes)}</span>
        </div>
      </section>

      <section style={{ marginTop: 'var(--s7)' }} aria-labelledby="history-title">
        <h2 id="history-title" className="h2" style={{ marginBottom: 'var(--s5)' }}>
          Mouvements
        </h2>
        {data.transactions.items.length === 0 ? (
          <div className="empty">
            <h3 className="h3">Aucun mouvement pour l’instant.</h3>
            <p className="muted">Les heures bougent quand un échange en banque de temps est confirmé par les deux personnes.</p>
            <Link to="/annonces?type=Service" className="btn btn--action">
              Voir les services proposés
            </Link>
          </div>
        ) : (
          <ul className="rows">
            {data.transactions.items.map((t) => (
              <li key={`${t.exchangeId}-${t.createdAt}`} className="row">
                <span className={`pill ${t.minutes > 0 ? 'pill--credit' : 'pill--debit'} tabular`}>{formatMinutes(t.minutes, { signed: true })}</span>
                <div>
                  <p className="row__title">
                    <Link to={`/echanges/${t.exchangeId}`}>{t.minutes > 0 ? 'Service rendu' : 'Service reçu'}</Link>
                  </p>
                  <p className="row__meta">{formatDate(t.createdAt)}</p>
                </div>
                <span aria-hidden="true">→</span>
              </li>
            ))}
          </ul>
        )}
        {data.transactions.totalPages > 1 && (
          <div className="pager form-actions">
            <button type="button" className="btn btn--small" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              Précédent
            </button>
            <button type="button" className="btn btn--small" disabled={page >= data.transactions.totalPages} onClick={() => setPage((p) => p + 1)}>
              Suivant
            </button>
          </div>
        )}
      </section>
    </div>
  )
}
