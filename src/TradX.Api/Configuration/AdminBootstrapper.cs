using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Users;

namespace TradX.Api.Configuration;

/// <summary>
/// Crée le premier administrateur : au démarrage, le compte dont l'e-mail est indiqué dans
/// <c>Bootstrap:AdminEmail</c> (variable d'environnement) est promu <see cref="UserRole.Admin"/>,
/// à condition d'exister et d'avoir confirmé son e-mail. L'opération est auditée.
/// </summary>
internal static class AdminBootstrapper
{
    /// <summary>Promeut l'administrateur configuré, s'il y en a un.</summary>
    /// <param name="app">Application.</param>
    /// <returns>Tâche asynchrone.</returns>
    public static async Task PromoteConfiguredAdminAsync(WebApplication app)
    {
        var configuredEmail = app.Configuration["Bootstrap:AdminEmail"];
        if (string.IsNullOrWhiteSpace(configuredEmail))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var email = User.NormalizeEmail(configuredEmail);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.EmailConfirmed || user.Role == UserRole.Admin)
        {
            return;
        }

        user.ChangeRole(UserRole.Admin);
        scope.ServiceProvider.GetRequiredService<IAuditLogger>()
            .Record("user.role_changed", nameof(User), user.Id, "bootstrap -> Admin");
        await db.SaveChangesAsync();
    }
}
