using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Domain.Users;

namespace TradX.Application.Auth;

/// <summary>Cas d'utilisation de l'authentification.</summary>
public interface IAuthService
{
    /// <summary>Inscrit un membre et lui envoie un lien de confirmation. Ne révèle pas si l'e-mail existe déjà.</summary>
    Task RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <summary>Confirme l'adresse e-mail avec le jeton reçu.</summary>
    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);

    /// <summary>Renvoie un lien de confirmation si le compte existe et n'est pas confirmé.</summary>
    Task ResendConfirmationAsync(EmailOnlyRequest request, CancellationToken ct);

    /// <summary>Connecte un membre et renvoie ses jetons.</summary>
    Task<AuthTokensResponse> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>Échange un refresh token contre une nouvelle paire de jetons (rotation).</summary>
    Task<AuthTokensResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);

    /// <summary>Déconnecte la session liée au refresh token (toute sa famille est révoquée).</summary>
    Task LogoutAsync(RefreshRequest request, CancellationToken ct);

    /// <summary>Envoie un lien de réinitialisation du mot de passe si le compte existe.</summary>
    Task ForgotPasswordAsync(EmailOnlyRequest request, CancellationToken ct);

    /// <summary>Change le mot de passe avec un jeton de réinitialisation et ferme toutes les sessions.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
}

/// <summary>
/// Implémentation de l'authentification. Points de sécurité :
/// messages génériques (pas d'énumération des comptes), temps de réponse homogène,
/// verrouillage après échecs, jetons opaques stockés hachés, rotation des refresh tokens
/// avec détection de réutilisation, audit de chaque événement.
/// </summary>
internal sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    IAccessTokenIssuer accessTokens,
    ISecureTokenGenerator tokens,
    IEmailSender emails,
    IAuditLogger audit,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IAuthService
{
    private const string GenericLoginError = "Identifiants invalides ou compte temporairement verrouillé.";
    private readonly AuthOptions _options = options.Value;
    private string? _dummyHash;

    /// <inheritdoc />
    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            // Même réponse ET même durée que pour une inscription réussie (le hachage coûte du temps) ;
            // le vrai titulaire est prévenu par e-mail.
            _ = hasher.Hash(request.Password);
            if (_options.RequireEmailConfirmation)
            {
                await emails.SendAsync(AuthEmails.AccountAlreadyExists(email, _options.FrontendBaseUrl), ct);
            }

            return;
        }

        var now = clock.GetUtcNow();
        var user = User.Register(email, request.DisplayName, hasher.Hash(request.Password), request.City, request.Neighborhood, now);
        string? rawToken = null;
        if (_options.RequireEmailConfirmation)
        {
            rawToken = IssueEmailConfirmation(user, now);
        }
        else
        {
            user.SkipEmailVerification();
        }

        db.Users.Add(user);
        audit.Record("auth.registered", nameof(User), user.Id, actorId: user.Id);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Deux inscriptions simultanées avec le même e-mail : l'index unique a protégé la base.
            return;
        }

        if (rawToken is not null)
        {
            await emails.SendAsync(AuthEmails.ConfirmEmail(email, _options.FrontendBaseUrl, rawToken), ct);
        }
    }

    /// <inheritdoc />
    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
    {
        var hash = tokens.Hash(request.Token);
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailConfirmationTokenHash == hash, ct)
            ?? throw InvalidToken();

        user.ConfirmEmail(clock.GetUtcNow());
        audit.Record("auth.email_confirmed", nameof(User), user.Id, actorId: user.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ResendConfirmationAsync(EmailOnlyRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (!_options.RequireEmailConfirmation || user is null || user.EmailConfirmed)
        {
            return;
        }

        var rawToken = IssueEmailConfirmation(user, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await emails.SendAsync(AuthEmails.ConfirmEmail(email, _options.FrontendBaseUrl, rawToken), ct);
    }

    /// <inheritdoc />
    public async Task<AuthTokensResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            // Hachage factice : la réponse prend le même temps que pour un compte existant.
            hasher.Verify(_dummyHash ??= hasher.Hash("tradx-dummy-password"), request.Password);
            audit.Record("auth.login_failed", nameof(User), null, "compte inconnu");
            await db.SaveChangesAsync(ct);
            throw new AuthenticationFailedException(GenericLoginError);
        }

        if (user.IsLockedOut(now))
        {
            audit.Record("auth.login_locked", nameof(User), user.Id, actorId: user.Id);
            await db.SaveChangesAsync(ct);
            throw new AuthenticationFailedException(GenericLoginError);
        }

        var check = hasher.Verify(user.PasswordHash, request.Password);
        if (check == PasswordCheck.Failed)
        {
            user.RegisterFailedLogin(now);
            audit.Record("auth.login_failed", nameof(User), user.Id, actorId: user.Id);
            await db.SaveChangesAsync(ct);
            throw new AuthenticationFailedException(GenericLoginError);
        }

        if (!_options.RequireEmailConfirmation && !user.EmailConfirmed)
        {
            // Compte créé quand la confirmation était exigée : en mode démonstration, il devient utilisable.
            user.SkipEmailVerification();
            audit.Record("auth.email_verification_skipped", nameof(User), user.Id, actorId: user.Id);
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(hasher.Hash(request.Password));
        }

        user.RegisterSuccessfulLogin();
        var response = IssueTokens(user, Guid.CreateVersion7(), now);
        audit.Record("auth.login_succeeded", nameof(User), user.Id, actorId: user.Id);
        await db.SaveChangesAsync(ct);
        return response;
    }

    /// <inheritdoc />
    public async Task<AuthTokensResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var current = await FindRefreshTokenAsync(request.RefreshToken, ct)
            ?? throw new AuthenticationFailedException("Session invalide. Reconnectez-vous.");

        if (current.WasAlreadyUsed)
        {
            // Un jeton déjà utilisé revient : il a probablement été volé. On coupe toute la session.
            await RevokeFamilyAsync(current.FamilyId, now, ct);
            audit.Record("auth.refresh_reuse_detected", nameof(User), current.UserId, actorId: current.UserId);
            await db.SaveChangesAsync(ct);
            throw new AuthenticationFailedException("Session invalide. Reconnectez-vous.");
        }

        var user = current.IsActive(now) ? await db.Users.FindAsync([current.UserId], ct) : null;
        if (user is null)
        {
            throw new AuthenticationFailedException("Session expirée. Reconnectez-vous.");
        }

        var response = IssueTokens(user, current.FamilyId, now, out var replacement);
        current.MarkReplaced(replacement.Id, now);
        await db.SaveChangesAsync(ct);
        return response;
    }

    /// <inheritdoc />
    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct)
    {
        var token = await FindRefreshTokenAsync(request.RefreshToken, ct);
        if (token is null)
        {
            return;
        }

        await RevokeFamilyAsync(token.FamilyId, clock.GetUtcNow(), ct);
        audit.Record("auth.logout", nameof(User), token.UserId, actorId: token.UserId);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ForgotPasswordAsync(EmailOnlyRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var rawToken = tokens.Generate();
        user.IssuePasswordResetToken(tokens.Hash(rawToken), now + _options.PasswordResetLifetime);
        audit.Record("auth.password_reset_requested", nameof(User), user.Id, actorId: user.Id);
        await db.SaveChangesAsync(ct);
        await emails.SendAsync(AuthEmails.ResetPassword(email, _options.FrontendBaseUrl, rawToken), ct);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = tokens.Hash(request.Token);
        var user = await db.Users.FirstOrDefaultAsync(u => u.PasswordResetTokenHash == hash, ct)
            ?? throw InvalidToken();

        user.ResetPassword(hasher.Hash(request.NewPassword), now);

        // Toutes les sessions existantes sont fermées : un éventuel voleur perd l'accès.
        var activeTokens = await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
        activeTokens.ForEach(t => t.Revoke(now));

        audit.Record("auth.password_reset", nameof(User), user.Id, actorId: user.Id);
        await db.SaveChangesAsync(ct);
    }

    private string IssueEmailConfirmation(User user, DateTimeOffset now)
    {
        var rawToken = tokens.Generate();
        user.IssueEmailConfirmationToken(tokens.Hash(rawToken), now + _options.EmailConfirmationLifetime);
        return rawToken;
    }

    private AuthTokensResponse IssueTokens(User user, Guid familyId, DateTimeOffset now) =>
        IssueTokens(user, familyId, now, out _);

    private AuthTokensResponse IssueTokens(User user, Guid familyId, DateTimeOffset now, out RefreshToken refreshToken)
    {
        var rawRefresh = tokens.Generate();
        refreshToken = RefreshToken.Issue(user.Id, tokens.Hash(rawRefresh), familyId, now, _options.RefreshTokenLifetime);
        db.RefreshTokens.Add(refreshToken);

        var access = accessTokens.Issue(user);
        return new AuthTokensResponse(access.Token, access.ExpiresAt, rawRefresh, refreshToken.ExpiresAt);
    }

    private Task<RefreshToken?> FindRefreshTokenAsync(string rawToken, CancellationToken ct)
    {
        var hash = tokens.Hash(rawToken);
        return db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        var family = await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(ct);
        family.ForEach(t => t.Revoke(now));
    }

    private static BusinessRuleException InvalidToken() =>
        new("auth.token_invalid", "Ce lien n'est pas valide ou a déjà été utilisé.");
}
