namespace TradX.Api.Security;

/// <summary>
/// Fonctions de compte disponibles, pour que le site n'affiche pas ce qui ne peut pas marcher
/// (ex. « mot de passe oublié » quand aucun e-mail ne peut partir). Ne contient aucun secret.
/// </summary>
/// <param name="EmailConfirmationRequired">Faut-il confirmer son adresse avant de publier et d'échanger ?</param>
/// <param name="PasswordResetAvailable">Le lien « mot de passe oublié » peut-il être envoyé ?</param>
public sealed record AuthSettingsResponse(bool EmailConfirmationRequired, bool PasswordResetAvailable);
