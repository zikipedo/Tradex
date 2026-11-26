namespace TradX.Application.Auth;

/// <summary>
/// Contenu des e-mails liés au compte. Le jeton n'apparaît que dans le lien envoyé au titulaire
/// de l'adresse ; il n'est jamais journalisé ni stocké en clair.
/// </summary>
internal static class AuthEmails
{
    /// <summary>E-mail de confirmation d'adresse.</summary>
    public static EmailMessage ConfirmEmail(string to, string frontendBaseUrl, string token) => new(
        to,
        "TradX : confirmez votre adresse e-mail",
        $"""
        Bienvenue sur TradX !

        Pour confirmer votre adresse e-mail, ouvrez ce lien (valable 24 heures) :
        {frontendBaseUrl.TrimEnd('/')}/confirmer-email?token={Uri.EscapeDataString(token)}

        Si vous n'êtes pas à l'origine de cette inscription, ignorez ce message.
        """);

    /// <summary>E-mail envoyé quand quelqu'un tente de s'inscrire avec une adresse déjà utilisée.</summary>
    public static EmailMessage AccountAlreadyExists(string to, string frontendBaseUrl) => new(
        to,
        "TradX : tentative d'inscription",
        $"""
        Quelqu'un a essayé de créer un compte TradX avec votre adresse, mais vous en avez déjà un.

        Mot de passe oublié ? {frontendBaseUrl.TrimEnd('/')}/mot-de-passe-oublie

        Si ce n'était pas vous, vous pouvez ignorer ce message.
        """);

    /// <summary>E-mail de réinitialisation du mot de passe.</summary>
    public static EmailMessage ResetPassword(string to, string frontendBaseUrl, string token) => new(
        to,
        "TradX : réinitialisation du mot de passe",
        $"""
        Pour choisir un nouveau mot de passe, ouvrez ce lien (valable 1 heure) :
        {frontendBaseUrl.TrimEnd('/')}/reinitialiser-mot-de-passe?token={Uri.EscapeDataString(token)}

        Si vous n'avez rien demandé, ignorez ce message : votre mot de passe reste inchangé.
        """);
}
