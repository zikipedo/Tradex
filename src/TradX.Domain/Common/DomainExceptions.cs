namespace TradX.Domain.Common;

/// <summary>
/// Exception métier de base. Chaque sous-type est traduit en code HTTP précis
/// par le gestionnaire d'erreurs unique de l'API (format ProblemDetails, sans stacktrace).
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>Crée une exception métier.</summary>
    /// <param name="code">Code stable, lisible par le front (ex. <c>exchange.invalid_transition</c>).</param>
    /// <param name="message">Message en français destiné à l'utilisateur.</param>
    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>Code d'erreur stable, utilisable par le front pour afficher un message adapté.</summary>
    public string Code { get; }
}

/// <summary>La ressource demandée n'existe pas ou n'est pas visible par l'utilisateur (HTTP 404).</summary>
/// <remarks>
/// On renvoie aussi 404 quand la ressource appartient à quelqu'un d'autre et qu'elle est privée :
/// cela évite de révéler son existence (protection contre l'énumération / IDOR).
/// </remarks>
public sealed class NotFoundException : DomainException
{
    /// <summary>Crée l'exception pour le type de ressource indiqué.</summary>
    /// <param name="resource">Nom de la ressource (ex. « Annonce »).</param>
    public NotFoundException(string resource)
        : base("resource.not_found", $"{resource} introuvable.")
    {
    }
}

/// <summary>L'utilisateur est authentifié mais n'a pas le droit de faire cette action (HTTP 403).</summary>
public sealed class ForbiddenException : DomainException
{
    /// <summary>Crée l'exception.</summary>
    /// <param name="message">Explication en français.</param>
    public ForbiddenException(string message = "Vous n'avez pas le droit d'effectuer cette action.")
        : base("access.forbidden", message)
    {
    }
}

/// <summary>Conflit avec l'état actuel des données : doublon, modification concurrente… (HTTP 409).</summary>
public sealed class ConflictException : DomainException
{
    /// <summary>Crée l'exception.</summary>
    /// <param name="code">Code d'erreur stable.</param>
    /// <param name="message">Explication en français.</param>
    public ConflictException(string code, string message) : base(code, message)
    {
    }
}

/// <summary>Une règle métier est violée : transition interdite, solde insuffisant… (HTTP 422).</summary>
public sealed class BusinessRuleException : DomainException
{
    /// <summary>Crée l'exception.</summary>
    /// <param name="code">Code d'erreur stable.</param>
    /// <param name="message">Explication en français.</param>
    public BusinessRuleException(string code, string message) : base(code, message)
    {
    }
}

/// <summary>Échec d'authentification : identifiants ou jeton invalides (HTTP 401).</summary>
public sealed class AuthenticationFailedException : DomainException
{
    /// <summary>Crée l'exception.</summary>
    /// <param name="message">Message volontairement générique (ne révèle pas si l'e-mail existe).</param>
    public AuthenticationFailedException(string message = "Identifiants invalides.")
        : base("auth.failed", message)
    {
    }
}
