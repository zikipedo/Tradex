using Microsoft.EntityFrameworkCore;
using TradX.Application.Exchanges;

namespace TradX.Api.Background;

/// <summary>
/// Tâche de fond : toutes les 10 minutes, fait expirer les propositions d'échange sans réponse depuis 7 jours.
/// Si plusieurs serveurs tournent, le verrouillage optimiste (xmin) empêche qu'une proposition soit traitée deux fois.
/// Désactivable par <c>Workers:ExchangeExpiration:Enabled=false</c> (tests).
/// </summary>
/// <param name="scopes">Fabrique de portées d'injection (un contexte de données neuf par passage).</param>
/// <param name="logger">Journal.</param>
internal sealed partial class ExchangeExpirationWorker(IServiceScopeFactory scopes, ILogger<ExchangeExpirationWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IExchangeMaintenance>().ExpireStaleProposalsAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Un autre serveur ou une action d'un membre a modifié l'échange en même temps : on réessaiera au prochain passage.
            LogConcurrency(logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailure(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Expiration des échanges : conflit de concurrence, nouvel essai au prochain passage")]
    private static partial void LogConcurrency(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Expiration des échanges : échec du passage")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
