using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TradX.IntegrationTests.Infrastructure;

namespace TradX.IntegrationTests.Platform;

/// <summary>
/// L'API sert le site React compilé (image de production) : adresses profondes, cache, CSP du site,
/// et l'API qui garde ses propres règles (JSON, CSP stricte, 404 sans HTML).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FrontendHostingTests(TradXApiFactory factory) : IDisposable
{
    private const string IndexMarker = "<div id=\"root\"></div>";
    private readonly string _webRoot = CreateFakeSite();

    private static string CreateFakeSite()
    {
        var root = Path.Combine(Path.GetTempPath(), "tradx-site-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "index.html"), $"<!doctype html><html lang=\"fr\"><body>{IndexMarker}</body></html>");
        File.WriteAllText(Path.Combine(root, "assets", "index-Ab12Cd34.js"), "console.info('tradx')");
        File.WriteAllText(Path.Combine(root, "favicon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        return root;
    }

    private HttpClient SiteClient() =>
        factory.WithWebHostBuilder(b => b.UseSetting(WebHostDefaults.WebRootKey, _webRoot))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Theory]
    [InlineData("/")]
    [InlineData("/annonces")]
    [InlineData("/annonces/0193f0a0-0000-7000-8000-000000000001")]
    [InlineData("/echanges")]
    public async Task Site_addresses_return_the_react_page_without_cache(string path)
    {
        var response = await SiteClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync()).ShouldContain(IndexMarker);
        response.Headers.CacheControl!.NoCache.ShouldBeTrue();
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.ShouldContain("script-src 'self'");
        csp.ShouldContain("frame-ancestors 'none'");
        csp.ShouldNotContain("unsafe-eval");
    }

    [Fact]
    public async Task Versioned_assets_are_cached_for_a_year()
    {
        var response = await SiteClient().GetAsync("/assets/index-Ab12Cd34.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromDays(365));
        response.Headers.CacheControl.Public.ShouldBeTrue();
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
    }

    [Fact]
    public async Task Api_keeps_json_and_its_strict_policy()
    {
        var client = SiteClient();

        var categories = await client.GetAsync("/api/v1/categories");
        categories.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        categories.Headers.GetValues("Content-Security-Policy").Single().ShouldBe("default-src 'none'; frame-ancestors 'none'");
        categories.Headers.CacheControl!.NoStore.ShouldBeTrue();

        // Une adresse d'API inconnue ne renvoie jamais la page du site.
        var unknown = await client.GetAsync("/api/v1/nexiste-pas");
        unknown.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
        (await unknown.Content.ReadAsStringAsync()).ShouldNotContain(IndexMarker);
    }

    [Fact]
    public async Task Only_get_requests_receive_the_site_page()
    {
        var response = await SiteClient().PostAsync("/annonces", content: null);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(IndexMarker);
    }

    [Fact]
    public async Task Without_a_built_site_nothing_changes()
    {
        // Configuration des autres tests (et du développement) : pas de wwwroot, Vite sert le site.
        var response = await factory.CreateClient().GetAsync("/annonces");

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
    }
}
