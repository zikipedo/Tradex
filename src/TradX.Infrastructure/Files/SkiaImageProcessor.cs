using SkiaSharp;
using TradX.Application.Listings;
using TradX.Domain.Common;

namespace TradX.Infrastructure.Files;

/// <summary>
/// Nettoyage des images avec SkiaSharp (licence MIT).
/// 1. vérifie les octets magiques (JPEG, PNG, WebP) : l'extension et le Content-Type du client ne comptent pas ;
/// 2. refuse les images démesurées (protection contre les « bombes de décompression ») ;
/// 3. applique l'orientation EXIF puis réencode en WebP : <b>toutes les métadonnées (dont le GPS) disparaissent</b> ;
/// 4. réduit la taille à <see cref="MaxDimension"/> pixels au plus (chargement rapide sur réseau mobile).
/// </summary>
internal sealed class SkiaImageProcessor : IImageProcessor
{
    /// <summary>Plus grand côté de l'image stockée.</summary>
    public const int MaxDimension = 1280;

    /// <summary>Nombre maximal de pixels acceptés en entrée (40 mégapixels).</summary>
    public const long MaxInputPixels = 40_000_000;

    private const int WebpQuality = 80;

    /// <inheritdoc />
    public ProcessedImage Process(byte[] content)
    {
        if (!HasAllowedSignature(content))
        {
            throw InvalidImage();
        }

        using var codec = SKCodec.Create(new SKMemoryStream(content)) ?? throw InvalidImage();
        if ((long)codec.Info.Width * codec.Info.Height > MaxInputPixels)
        {
            throw new BusinessRuleException("photo.too_large", "Cette image est trop grande (40 mégapixels maximum).");
        }

        using var decoded = SKBitmap.Decode(codec) ?? throw InvalidImage();
        using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);
        using var resized = Resize(oriented);
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality) ?? throw InvalidImage();

        return new ProcessedImage(encoded.ToArray(), resized.Width, resized.Height);
    }

    /// <summary>Vérifie la signature binaire du fichier (JPEG, PNG ou WebP uniquement).</summary>
    /// <param name="bytes">Contenu.</param>
    /// <returns>Vrai si le format est autorisé.</returns>
    internal static bool HasAllowedSignature(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> jpeg = [0xFF, 0xD8, 0xFF];
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var isWebp = bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8);

        return bytes.StartsWith(jpeg) || bytes.StartsWith(png) || isWebp;
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var (rotation, swap) = origin switch
        {
            SKEncodedOrigin.BottomRight => (180f, false),
            SKEncodedOrigin.RightTop => (90f, true),
            SKEncodedOrigin.LeftBottom => (270f, true),
            _ => (0f, false),
        };

        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;
        var result = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        canvas.Translate(width / 2f, height / 2f);
        canvas.RotateDegrees(rotation);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    private static SKBitmap Resize(SKBitmap source)
    {
        var scale = Math.Min(1d, (double)MaxDimension / Math.Max(source.Width, source.Height));
        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)),
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        return source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw InvalidImage();
    }

    private static BusinessRuleException InvalidImage() =>
        new("photo.invalid_format", "Le fichier n'est pas une image valide (JPEG, PNG ou WebP).");
}
