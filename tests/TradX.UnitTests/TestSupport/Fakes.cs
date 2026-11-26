using System.Text.RegularExpressions;
using NSubstitute;
using TradX.Application.Auth;
using TradX.Application.Common;
using TradX.Domain.Users;

namespace TradX.UnitTests.TestSupport;

/// <summary>Garde en mémoire les e-mails « envoyés » pour pouvoir lire les liens dans les tests.</summary>
internal sealed partial class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>Extrait le jeton du dernier e-mail envoyé à l'adresse donnée.</summary>
    public string LastTokenFor(string email)
    {
        var message = Sent.Last(m => m.To == email);
        return Uri.UnescapeDataString(TokenRegex().Match(message.Body).Groups[1].Value);
    }

    [GeneratedRegex(@"token=([^\s]+)")]
    private static partial Regex TokenRegex();
}

/// <summary>Émetteur de JWT factice : renvoie une valeur lisible et prévisible.</summary>
internal sealed class FakeAccessTokenIssuer(TimeProvider clock) : IAccessTokenIssuer
{
    public AccessToken Issue(User user) => new($"access-{user.Id}", clock.GetUtcNow().AddMinutes(15));
}

/// <summary>Fabrique d'utilisateur courant simulé.</summary>
internal static class CurrentUserFactory
{
    public static ICurrentUser For(Guid? userId)
    {
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(userId);
        current.RequiredUserId.Returns(_ => userId ?? throw new TradX.Domain.Common.AuthenticationFailedException());
        current.IpAddress.Returns("127.0.0.1");
        return current;
    }
}
