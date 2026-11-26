using Microsoft.Extensions.Options;
using TradX.Application.Listings;

namespace TradX.Infrastructure.Files;

/// <summary>Paramètres du stockage de fichiers (section <c>Storage</c>).</summary>
public sealed class StorageOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "Storage";

    /// <summary>Dossier racine du stockage local (hors du dossier web, jamais servi directement).</summary>
    public string RootPath { get; set; } = "uploads";
}

/// <summary>
/// Stockage sur disque local (développement et petit déploiement). Les clés sont générées par le serveur ;
/// on vérifie malgré tout que le chemin final reste dans le dossier racine (protection « path traversal »).
/// En production, une implémentation compatible S3 pourra la remplacer sans toucher au reste du code.
/// </summary>
/// <param name="options">Paramètres.</param>
internal sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    /// <inheritdoc />
    public async Task SaveAsync(string key, byte[] content, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Transforme une clé en chemin absolu, en refusant toute sortie du dossier racine.</summary>
    /// <param name="key">Clé relative (ex. <c>listings/{id}/{photo}.webp</c>).</param>
    /// <returns>Chemin absolu.</returns>
    /// <exception cref="ArgumentException">Si la clé sort du dossier racine.</exception>
    internal string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Clé de fichier invalide.", nameof(key));
        }

        return path;
    }
}
