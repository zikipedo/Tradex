using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradX.Application.Auth;

namespace TradX.Infrastructure.Email;

/// <summary>Paramètres d'envoi des e-mails (section <c>Email</c>).</summary>
public sealed class EmailOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "Email";

    /// <summary>Valeur de <see cref="Mode"/> qui désactive tout envoi (projet de démonstration).</summary>
    public const string DisabledMode = "Disabled";

    /// <summary>
    /// <c>File</c> (développement : fichiers locaux), <c>Smtp</c> (production) ou <c>Disabled</c>
    /// (démonstration : aucun e-mail, « mot de passe oublié » indisponible).
    /// </summary>
    public string Mode { get; set; } = "File";

    /// <summary>Dossier des e-mails en mode fichier (ignoré par Git).</summary>
    public string OutboxDirectory { get; set; } = ".mail-outbox";

    /// <summary>Adresse d'expédition.</summary>
    public string From { get; set; } = "no-reply@tradx.local";

    /// <summary>Serveur SMTP.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>Port SMTP (587 = STARTTLS).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Utilisateur SMTP (variable d'environnement).</summary>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>Mot de passe SMTP (variable d'environnement, jamais dans le code).</summary>
    public string SmtpPassword { get; set; } = string.Empty;
}

/// <summary>
/// Développement : écrit chaque e-mail dans un fichier texte du dossier <see cref="EmailOptions.OutboxDirectory"/>.
/// Permet de tester les liens de confirmation sans serveur de mail et sans rien écrire dans les logs.
/// </summary>
/// <param name="options">Paramètres.</param>
internal sealed class FileEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(options.Value.OutboxDirectory);
        Directory.CreateDirectory(directory);
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt";
        var content = $"To: {message.To}\nSubject: {message.Subject}\n\n{message.Body}\n";
        await File.WriteAllTextAsync(Path.Combine(directory, fileName), content, Encoding.UTF8, cancellationToken);
    }
}

/// <summary>
/// Mode démonstration (<c>Email:Mode = Disabled</c>) : aucun e-mail n'est envoyé ni écrit.
/// Seul le sujet est journalisé, jamais l'adresse ni le contenu (qui peut contenir un lien secret).
/// </summary>
/// <param name="logger">Journal.</param>
internal sealed partial class DisabledEmailSender(ILogger<DisabledEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        LogSkipped(logger, message.Subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Envoi d'e-mails désactivé : message « {Subject} » non envoyé.")]
    private static partial void LogSkipped(ILogger logger, string subject);
}

/// <summary>Production : envoi SMTP chiffré (STARTTLS). Les identifiants viennent de l'environnement.</summary>
/// <param name="options">Paramètres.</param>
/// <param name="logger">Journal (jamais le contenu du message ni l'adresse complète).</param>
internal sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPassword),
        };
        using var mail = new MailMessage(settings.From, message.To, message.Subject, message.Body);

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (SmtpException ex)
        {
            LogSendFailed(logger, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Échec d'envoi d'un e-mail via SMTP")]
    private static partial void LogSendFailed(ILogger logger, Exception exception);
}
