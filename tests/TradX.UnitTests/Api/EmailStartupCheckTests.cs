using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using TradX.Api.Configuration;

namespace TradX.UnitTests.Api;

/// <summary>Règles de démarrage sur l'envoi des e-mails en production.</summary>
public sealed class EmailStartupCheckTests
{
    private static IWebHostEnvironment Env(string name)
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns(name);
        return env;
    }

    private static IConfiguration Config(string? mode, bool? requireConfirmation) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Mode"] = mode,
            ["Auth:RequireEmailConfirmation"] = requireConfirmation?.ToString(),
        })
        .Build();

    [Theory]
    [InlineData("Smtp", true)]
    [InlineData("Smtp", false)]
    [InlineData("Disabled", false)]
    [InlineData("disabled", false)]
    public void Production_accepts_smtp_or_disabled_emails_in_demo_mode(string mode, bool requireConfirmation)
    {
        Should.NotThrow(() => ApiServiceCollectionExtensions.EnsureProductionEmailIsReal(Config(mode, requireConfirmation), Env(Environments.Production)));
    }

    [Theory]
    [InlineData("File", false)]
    [InlineData(null, false)]
    [InlineData("Disabled", true)]
    [InlineData("Disabled", null)]
    public void Production_refuses_local_files_and_disabled_emails_when_confirmation_is_required(string? mode, bool? requireConfirmation)
    {
        Should.Throw<InvalidOperationException>(() =>
            ApiServiceCollectionExtensions.EnsureProductionEmailIsReal(Config(mode, requireConfirmation), Env(Environments.Production)));
    }

    [Fact]
    public void Development_accepts_every_mode()
    {
        Should.NotThrow(() => ApiServiceCollectionExtensions.EnsureProductionEmailIsReal(Config("File", true), Env(Environments.Development)));
    }
}
