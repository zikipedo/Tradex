using TradX.Application.Auth;
using TradX.Application.Users;
using TradX.Domain.Users;

namespace TradX.UnitTests.Application;

/// <summary>Validation stricte des entrées d'identité (OWASP : valider côté serveur).</summary>
public sealed class AuthValidatorsTests
{
    private static RegisterRequest Valid() => new("awa@example.ml", "un-long-mot-de-passe", "Awa", "Bamako", "Badalabougou");

    [Fact]
    public void Valid_registration_passes()
    {
        new RegisterRequestValidator().Validate(Valid()).IsValid.ShouldBeTrue();
    }

    public static TheoryData<RegisterRequest, string> InvalidRegistrations => new()
    {
        { Valid() with { Email = "" }, "Email" },
        { Valid() with { Email = "pas-un-email" }, "Email" },
        { Valid() with { Email = new string('a', 250) + "@x.ml" }, "Email" },
        { Valid() with { Password = "court" }, "Password" },
        { Valid() with { Password = new string('p', 129) }, "Password" },
        { Valid() with { Password = "awa@example.ml" }, "Password" },
        { Valid() with { DisplayName = "   " }, "DisplayName" },
        { Valid() with { DisplayName = new string('n', 61) }, "DisplayName" },
        { Valid() with { City = "" }, "City" },
        { Valid() with { Neighborhood = new string('q', 81) }, "Neighborhood" },
    };

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public void Invalid_registration_is_rejected_on_the_right_field(RegisterRequest request, string field)
    {
        var result = new RegisterRequestValidator().Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == field);
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("a@b.ml", "")]
    public void Login_requires_email_and_password(string email, string password)
    {
        new LoginRequestValidator().Validate(new LoginRequest(email, password)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Tokens_longer_than_max_are_rejected()
    {
        var tooLong = new string('t', 129);

        new RefreshRequestValidator().Validate(new RefreshRequest(tooLong)).IsValid.ShouldBeFalse();
        new ConfirmEmailRequestValidator().Validate(new ConfirmEmailRequest(tooLong)).IsValid.ShouldBeFalse();
        new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(tooLong, "un-long-mot-de-passe")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Reset_password_enforces_new_password_rules()
    {
        new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest("tok", "court")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Profile_bio_is_limited_to_500_characters()
    {
        var validator = new UpdateProfileRequestValidator();

        validator.Validate(new UpdateProfileRequest("Awa", "Bamako", null, new string('b', 500))).IsValid.ShouldBeTrue();
        validator.Validate(new UpdateProfileRequest("Awa", "Bamako", null, new string('b', 501))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Unknown_role_value_is_rejected()
    {
        new ChangeRoleRequestValidator().Validate(new ChangeRoleRequest((UserRole)42)).IsValid.ShouldBeFalse();
    }
}
