using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using TradX.Application.Auth;
using TradX.Domain.Users;
using TradX.Infrastructure.Security;

namespace TradX.UnitTests.Infrastructure;

/// <summary>Tests des briques cryptographiques : hachage, jetons opaques, JWT.</summary>
public sealed class SecurityServicesTests
{
    [Fact]
    public void Password_hash_is_salted_and_verifiable()
    {
        var hasher = new AspNetPasswordHasher();

        var first = hasher.Hash("mot-de-passe-solide");
        var second = hasher.Hash("mot-de-passe-solide");

        first.ShouldNotBe(second);
        hasher.Verify(first, "mot-de-passe-solide").ShouldBe(PasswordCheck.Success);
        hasher.Verify(first, "autre").ShouldBe(PasswordCheck.Failed);
    }

    [Fact]
    public void Opaque_tokens_are_unique_url_safe_and_hashed_deterministically()
    {
        var generator = new SecureTokenGenerator();

        var tokens = Enumerable.Range(0, 100).Select(_ => generator.Generate()).ToList();

        tokens.Distinct().Count().ShouldBe(100);
        tokens.ShouldAllBe(t => t.Length == 43 && !t.Contains('+') && !t.Contains('/') && !t.Contains('='));
        generator.Hash(tokens[0]).ShouldBe(generator.Hash(tokens[0]));
        generator.Hash(tokens[0]).Length.ShouldBe(64);
    }

    [Fact]
    public void Access_token_contains_subject_and_role_but_no_personal_data()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero));
        var issuer = new JwtAccessTokenIssuer(
            Options.Create(new JwtOptions { SigningKey = new string('k', 32) }),
            Options.Create(new AuthOptions()),
            clock);
        var user = User.Register("awa@example.ml", "Awa", "hash", "Bamako", null, clock.GetUtcNow());

        var token = issuer.Issue(user);

        var jwt = new JsonWebToken(token.Token);
        jwt.Subject.ShouldBe(user.Id.ToString());
        jwt.GetClaim("role").Value.ShouldBe("User");
        jwt.Alg.ShouldBe("HS256");
        jwt.ValidTo.ShouldBe(clock.GetUtcNow().AddMinutes(15).UtcDateTime);
        token.Token.ShouldNotContain("awa");
        jwt.Claims.ShouldNotContain(c => c.Value.Contains("awa@example.ml", StringComparison.Ordinal));
    }
}
