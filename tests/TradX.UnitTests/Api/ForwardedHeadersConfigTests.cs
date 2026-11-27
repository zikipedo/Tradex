using Microsoft.Extensions.Configuration;
using TradX.Api.Security;

namespace TradX.UnitTests.Api;

/// <summary>La confiance dans X-Forwarded-For n'est accordée qu'à des proxys ou réseaux déclarés.</summary>
public sealed class ForwardedHeadersConfigTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) => new ConfigurationBuilder()
        .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
        .Build();

    [Fact]
    public void Nothing_declared_means_nobody_is_trusted()
    {
        SecuritySetup.HasTrustedProxies(Config()).ShouldBeFalse();
    }

    [Fact]
    public void A_proxy_address_or_a_network_enables_forwarded_headers()
    {
        SecuritySetup.HasTrustedProxies(Config(("ForwardedHeaders:KnownProxies:0", "10.0.0.5"))).ShouldBeTrue();
        SecuritySetup.HasTrustedProxies(Config(("ForwardedHeaders:KnownNetworks:0", "172.16.0.0/12"))).ShouldBeTrue();
    }
}
