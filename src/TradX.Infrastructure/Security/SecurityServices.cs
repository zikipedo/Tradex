using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TradX.Application.Auth;
using TradX.Domain.Users;

namespace TradX.Infrastructure.Security;

/// <summary>Paramètres de signature des JWT (section <c>Jwt</c>). La clé vient de l'environnement, jamais du code.</summary>
public sealed class JwtOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Nom du claim portant le rôle.</summary>
    public const string RoleClaim = "role";

    /// <summary>Longueur minimale de la clé HMAC-SHA256 (256 bits).</summary>
    public const int MinKeyBytes = 32;

    /// <summary>Émetteur des jetons.</summary>
    public string Issuer { get; set; } = "tradx-api";

    /// <summary>Public visé par les jetons.</summary>
    public string Audience { get; set; } = "tradx-web";

    /// <summary>Clé secrète de signature (au moins 32 octets).</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Clé de signature sous forme utilisable par la bibliothèque JWT.</summary>
    /// <returns>Clé symétrique.</returns>
    public SymmetricSecurityKey CreateSecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

/// <summary>
/// Hachage des mots de passe avec <see cref="PasswordHasher{TUser}"/> d'ASP.NET Core Identity
/// (PBKDF2-HMAC-SHA512, 100 000 itérations, sel aléatoire) : algorithme éprouvé, aucun hachage « maison ».
/// </summary>
internal sealed class AspNetPasswordHasher : Application.Auth.IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    /// <inheritdoc />
    public string Hash(string password) => _inner.HashPassword(null!, password);

    /// <inheritdoc />
    public PasswordCheck Verify(string hash, string password) => _inner.VerifyHashedPassword(null!, hash, password) switch
    {
        PasswordVerificationResult.Success => PasswordCheck.Success,
        PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
        _ => PasswordCheck.Failed,
    };
}

/// <summary>Jetons opaques : 256 bits aléatoires (générateur cryptographique), stockés sous forme SHA-256.</summary>
internal sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    /// <inheritdoc />
    public string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <inheritdoc />
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Émission des JWT d'accès, signés en HMAC-SHA256.</summary>
/// <param name="jwtOptions">Clé, émetteur, public.</param>
/// <param name="authOptions">Durée de vie.</param>
/// <param name="clock">Horloge.</param>
internal sealed class JwtAccessTokenIssuer(
    IOptions<JwtOptions> jwtOptions,
    IOptions<AuthOptions> authOptions,
    TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    /// <inheritdoc />
    public AccessToken Issue(User user)
    {
        var options = jwtOptions.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now + authOptions.Value.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            // Volontairement minimal : ni e-mail ni donnée personnelle dans le jeton (il est lisible en Base64).
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtOptions.RoleClaim, user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(options.CreateSecurityKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
