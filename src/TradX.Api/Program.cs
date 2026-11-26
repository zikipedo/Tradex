using TradX.Api.Configuration;
using TradX.Application;
using TradX.Infrastructure;

// Point d'entrée de l'API TradX.
// La configuration est découpée en méthodes d'extension lisibles (voir le dossier Configuration).
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Ne pas annoncer le serveur utilisé (réduit l'information donnée à un attaquant).
    kestrel.AddServerHeader = false;
    // Taille maximale d'une requête : 1 Mo (les endpoints d'upload ont leur propre limite).
    kestrel.Limits.MaxRequestBodySize = 1 * 1024 * 1024;
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

await app.UseTradXPipelineAsync();
await app.RunAsync();
