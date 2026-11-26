using System.Text;
using SkiaSharp;

namespace TradX.UnitTests.TestSupport;

/// <summary>Fabrique d'images de test générées en mémoire (aucun fichier réel).</summary>
internal static class TestImages
{
    /// <summary>Marqueur placé dans les fausses métadonnées GPS pour vérifier leur suppression.</summary>
    public const string GpsMarker = "GPSSECRET-12.6392N-8.0029W";

    public static byte[] Png(int width = 64, int height = 48) => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width = 64, int height = 48) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    /// <summary>JPEG contenant un segment APP1 « Exif » avec une fausse position GPS.</summary>
    public static byte[] JpegWithGpsMetadata()
    {
        var jpeg = Jpeg();
        var payload = Encoding.ASCII.GetBytes("Exif\0\0").Concat(Encoding.ASCII.GetBytes(GpsMarker)).ToArray();
        var length = payload.Length + 2;
        byte[] segment = [0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF), .. payload];
        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Orange);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
