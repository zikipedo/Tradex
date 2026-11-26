using TradX.Domain.Common;
using TradX.Domain.Users;

namespace TradX.Domain.Exchanges;

/// <summary>
/// Mouvement de crédits de temps entre deux membres, enregistré à la fin d'un échange « banque de temps ».
/// Le grand livre est <b>append-only</b> : jamais modifié ni supprimé. Comme chaque mouvement débite
/// exactement ce qu'il crédite, la somme de tous les soldes reste toujours égale à zéro.
/// </summary>
public sealed class TimeTransaction : Entity
{
    private TimeTransaction()
    {
    }

    internal TimeTransaction(Guid exchangeId, Guid fromUserId, Guid toUserId, int minutes, DateTimeOffset createdAt)
    {
        ExchangeId = exchangeId;
        FromUserId = fromUserId;
        ToUserId = toUserId;
        Minutes = minutes;
        CreatedAt = createdAt;
    }

    /// <summary>Échange à l'origine du mouvement (unique : un échange ne paie qu'une fois).</summary>
    public Guid ExchangeId { get; private set; }

    /// <summary>Membre débité (bénéficiaire du service).</summary>
    public Guid FromUserId { get; private set; }

    /// <summary>Membre crédité (celui qui a rendu le service).</summary>
    public Guid ToUserId { get; private set; }

    /// <summary>Durée transférée, en minutes (toujours positive).</summary>
    public int Minutes { get; private set; }

    /// <summary>Date du mouvement.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>
/// Règles de la banque de temps. <b>Ce n'est pas de l'argent</b> : 1 heure de service = 1 heure de crédit,
/// quel que soit le service. Les crédits ne s'achètent pas, ne se vendent pas et ne se retirent pas.
/// </summary>
public static class TimeBank
{
    /// <summary>Découvert autorisé : −5 heures, pour que les nouveaux membres puissent démarrer.</summary>
    public const int OverdraftLimitMinutes = -300;

    /// <summary>Plafond : +100 heures, pour éviter la thésaurisation.</summary>
    public const int MaxBalanceMinutes = 6000;

    /// <summary>Granularité d'un service : 30 minutes.</summary>
    public const int SlotMinutes = 30;

    /// <summary>Durée maximale d'un service : 8 heures.</summary>
    public const int MaxServiceMinutes = 480;

    /// <summary>Vérifie qu'une durée de service est valide (multiple de 30 min, entre 30 min et 8 h).</summary>
    /// <param name="minutes">Durée proposée.</param>
    /// <exception cref="BusinessRuleException">Si la durée est invalide.</exception>
    public static void EnsureValidDuration(int minutes)
    {
        if (minutes < SlotMinutes || minutes > MaxServiceMinutes || minutes % SlotMinutes != 0)
        {
            throw new BusinessRuleException("timebank.invalid_duration",
                "La durée doit être un multiple de 30 minutes, entre 30 minutes et 8 heures.");
        }
    }

    /// <summary>Vérifie que le bénéficiaire peut être débité et le prestataire crédité sans dépasser les limites.</summary>
    /// <param name="beneficiary">Membre débité.</param>
    /// <param name="provider">Membre crédité.</param>
    /// <param name="minutes">Durée.</param>
    /// <exception cref="BusinessRuleException">Si une limite serait dépassée.</exception>
    public static void EnsureTransferAllowed(User beneficiary, User provider, int minutes)
    {
        if (beneficiary.TimeBalanceMinutes - minutes < OverdraftLimitMinutes)
        {
            throw new BusinessRuleException("timebank.insufficient_balance",
                "Solde de temps insuffisant : le découvert est limité à 5 heures. Rendez d'abord un service.");
        }

        if (provider.TimeBalanceMinutes + minutes > MaxBalanceMinutes)
        {
            throw new BusinessRuleException("timebank.balance_cap_reached",
                "Le prestataire a atteint le plafond de 100 heures : il doit d'abord utiliser ses crédits.");
        }
    }

    /// <summary>Transfère des minutes du bénéficiaire vers le prestataire et crée l'écriture du grand livre.</summary>
    /// <param name="exchangeId">Échange concerné.</param>
    /// <param name="beneficiary">Membre débité.</param>
    /// <param name="provider">Membre crédité.</param>
    /// <param name="minutes">Durée.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>Écriture à enregistrer.</returns>
    public static TimeTransaction Transfer(Guid exchangeId, User beneficiary, User provider, int minutes, DateTimeOffset now)
    {
        EnsureValidDuration(minutes);
        if (beneficiary.Id == provider.Id)
        {
            throw new BusinessRuleException("timebank.self_transfer", "Impossible de se transférer du temps à soi-même.");
        }

        EnsureTransferAllowed(beneficiary, provider, minutes);
        beneficiary.ApplyTimeDelta(-minutes);
        provider.ApplyTimeDelta(minutes);
        return new TimeTransaction(exchangeId, beneficiary.Id, provider.Id, minutes, now);
    }
}
