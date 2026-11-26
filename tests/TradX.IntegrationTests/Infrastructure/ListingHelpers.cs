using System.Net.Http.Headers;
using System.Net.Http.Json;
using SkiaSharp;
using TradX.Application.Listings;
using TradX.Domain.Listings;

namespace TradX.IntegrationTests.Infrastructure;

/// <summary>Raccourcis pour créer des annonces et des images de test.</summary>
public static class ListingHelpers
{
    /// <summary>Catégorie « Électronique » créée par la migration.</summary>
    public static readonly Guid ElectronicsCategoryId = new("0193f0a0-0000-7000-8000-000000000001");

    /// <summary>Catégorie « Cours et soutien scolaire » créée par la migration.</summary>
    public static readonly Guid LessonsCategoryId = new("0193f0a0-0000-7000-8000-000000000004");

    /// <summary>Demande d'annonce valide.</summary>
    public static UpsertListingRequest NewListing(
        string title = "Vélo de ville",
        ListingType type = ListingType.Good,
        ListingDirection direction = ListingDirection.Offer,
        bool donation = false,
        Guid? categoryId = null,
        string city = "Bamako") => new(
        direction, type, title, $"{title} : en bon état, disponible rapidement.", categoryId ?? ElectronicsCategoryId,
        type == ListingType.Good ? ItemCondition.Good : null, city, "Badalabougou", "Le soir", donation ? null : "Une perceuse", donation);

    /// <summary>Crée un brouillon et renvoie l'annonce.</summary>
    public static async Task<ListingResponse> CreateDraftAsync(this TestMember member, UpsertListingRequest? request = null)
    {
        var response = await member.Client.PostAsJsonAsync("/api/v1/listings", request ?? NewListing(), Json.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ListingResponse>(Json.Options))!;
    }

    /// <summary>Crée et publie une annonce.</summary>
    public static async Task<ListingResponse> CreatePublishedAsync(this TestMember member, UpsertListingRequest? request = null)
    {
        var draft = await member.CreateDraftAsync(request);
        var response = await member.Client.PostAsync($"/api/v1/listings/{draft.Id}/publish", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ListingResponse>(Json.Options))!;
    }

    /// <summary>Envoie un fichier en multipart comme le ferait un navigateur.</summary>
    public static Task<HttpResponseMessage> UploadPhotoAsync(this HttpClient client, Guid listingId, byte[] content,
        string fileName = "photo.png", string contentType = "image/png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return client.PostAsync($"/api/v1/listings/{listingId}/photos", form);
    }

    /// <summary>Petite image PNG générée en mémoire.</summary>
    public static byte[] Png(int width = 40, int height = 30)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SeaGreen);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
