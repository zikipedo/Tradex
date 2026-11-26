using FluentValidation;

namespace TradX.Application.Auth;

/// <summary>Règles communes de validation des champs d'identité.</summary>
internal static class IdentityRules
{
    /// <summary>Longueur minimale d'un mot de passe (recommandation OWASP ASVS : longueur plutôt que complexité).</summary>
    public const int PasswordMinLength = 10;

    /// <summary>Longueur maximale (évite les attaques par mot de passe géant sur le hachage).</summary>
    public const int PasswordMaxLength = 128;

    /// <summary>Longueur maximale d'un e-mail (RFC 5321).</summary>
    public const int EmailMaxLength = 254;

    /// <summary>Longueur maximale d'un jeton opaque reçu par e-mail.</summary>
    public const int TokenMaxLength = 128;

    /// <summary>Applique les règles d'un e-mail.</summary>
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("L'e-mail est obligatoire.")
        .MaximumLength(EmailMaxLength).WithMessage("L'e-mail est trop long.")
        .EmailAddress().WithMessage("L'e-mail n'est pas valide.");

    /// <summary>Applique les règles d'un nouveau mot de passe.</summary>
    public static IRuleBuilderOptions<T, string> ValidNewPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Le mot de passe est obligatoire.")
        .MinimumLength(PasswordMinLength).WithMessage($"Le mot de passe doit contenir au moins {PasswordMinLength} caractères.")
        .MaximumLength(PasswordMaxLength).WithMessage($"Le mot de passe ne doit pas dépasser {PasswordMaxLength} caractères.");

    /// <summary>Applique les règles d'un jeton opaque.</summary>
    public static IRuleBuilderOptions<T, string> ValidToken<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Le jeton est obligatoire.")
        .MaximumLength(TokenMaxLength).WithMessage("Le jeton est invalide.");

    /// <summary>Applique les règles d'un texte court obligatoire (nom, ville).</summary>
    public static IRuleBuilderOptions<T, string> RequiredText<T>(this IRuleBuilder<T, string> rule, string label, int max) => rule
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage($"{label} est obligatoire.")
        .MaximumLength(max).WithMessage($"{label} ne doit pas dépasser {max} caractères.");
}

/// <summary>Validation de l'inscription.</summary>
internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password).ValidNewPassword()
            .Must((r, password) => !string.Equals(password, r.Email, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Le mot de passe ne doit pas être identique à l'e-mail.");
        RuleFor(r => r.DisplayName).RequiredText("Le nom affiché", 60);
        RuleFor(r => r.City).RequiredText("La ville", 80);
        RuleFor(r => r.Neighborhood).MaximumLength(80).WithMessage("Le quartier ne doit pas dépasser 80 caractères.");
    }
}

/// <summary>Validation de la connexion (pas de règle de longueur minimale : on ne donne pas d'indice).</summary>
internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(IdentityRules.EmailMaxLength);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(IdentityRules.PasswordMaxLength);
    }
}

/// <summary>Validation du renouvellement de jetons.</summary>
internal sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(r => r.RefreshToken).ValidToken();
}

/// <summary>Validation de la confirmation d'e-mail.</summary>
internal sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator() => RuleFor(r => r.Token).ValidToken();
}

/// <summary>Validation des demandes ne contenant qu'un e-mail.</summary>
internal sealed class EmailOnlyRequestValidator : AbstractValidator<EmailOnlyRequest>
{
    public EmailOnlyRequestValidator() => RuleFor(r => r.Email).ValidEmail();
}

/// <summary>Validation de la réinitialisation du mot de passe.</summary>
internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(r => r.Token).ValidToken();
        RuleFor(r => r.NewPassword).ValidNewPassword();
    }
}
