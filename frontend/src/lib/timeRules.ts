/** Règles de la banque de temps, identiques à celles de l'API (`TimeBank` côté domaine). */
export const TIME_RULES = {
  /** Découvert autorisé : −5 h. */
  overdraftLimitMinutes: -300,
  /** Plafond : +100 h. */
  maxBalanceMinutes: 6000,
  /** Unité : 30 minutes. */
  slotMinutes: 30,
  /** Durée maximale d'un service : 8 h. */
  maxServiceMinutes: 480,
} as const
