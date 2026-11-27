using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using TradX.Application.Common;
using TradX.Application.Listings;
using TradX.Domain.Listings;
using TradX.Domain.Users;
using TradX.IntegrationTests.Infrastructure;

namespace TradX.IntegrationTests.Listings;

/// <summary>CRUD des annonces, recherche, photos, catégories et contrôles d'accès (IDOR, rôles).</summary>
[Collection(ApiCollection.Name)]
public sealed class ListingEndpointsTests(TradXApiFactory factory)
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    public async Task Full_crud_flow_create_read_update_publish_search_archive()
    {
        var alice = await factory.CreateMemberAsync();
        var uniqueTitle = $"Guitare {Guid.NewGuid():N}"[..30];

        var created = await alice.Client.PostAsJsonAsync("/api/v1/listings", ListingHelpers.NewListing(uniqueTitle), Json.Options);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.Location!.ToString().ShouldContain("/api/v1/listings/");
        var listing = (await created.Content.ReadFromJsonAsync<ListingResponse>(Json.Options))!;
        listing.Status.ShouldBe(ListingStatus.Draft);

        var updated = await alice.Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}",
            ListingHelpers.NewListing(uniqueTitle) with { Availability = "Samedi matin" }, Json.Options);
        (await updated.Content.ReadFromJsonAsync<ListingResponse>(Json.Options))!.Availability.ShouldBe("Samedi matin");

        await alice.Client.PostAsync($"/api/v1/listings/{listing.Id}/publish", null);
        var found = await _anonymous.GetFromJsonAsync<PagedResult<ListingSummary>>($"/api/v1/listings?q={Uri.EscapeDataString(uniqueTitle)}", Json.Options);
        found!.Items.Single().Id.ShouldBe(listing.Id);

        (await alice.Client.PostAsync($"/api/v1/listings/{listing.Id}/archive", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterArchive = await _anonymous.GetFromJsonAsync<PagedResult<ListingSummary>>($"/api/v1/listings?q={Uri.EscapeDataString(uniqueTitle)}", Json.Options);
        afterArchive!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unconfirmed_member_cannot_create_listing()
    {
        var unconfirmed = await factory.CreateMemberAsync(confirmEmail: false);

        var response = await unconfirmed.Client.PostAsJsonAsync("/api/v1/listings", ListingHelpers.NewListing(), Json.Options);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Extensions["code"]!.ToString().ShouldBe("user.email_not_confirmed");
    }

    [Fact]
    public async Task Anonymous_cannot_create_listing()
    {
        (await _anonymous.PostAsJsonAsync("/api/v1/listings", ListingHelpers.NewListing(), Json.Options))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_listing_returns_400_with_field_errors()
    {
        var alice = await factory.CreateMemberAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/v1/listings",
            ListingHelpers.NewListing("Vél") with { Description = "court", City = "" }, Json.Options);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.ShouldBe(["title", "description", "city"], ignoreOrder: true);
    }

    [Fact]
    public async Task There_is_no_price_field_and_sending_one_is_rejected()
    {
        var alice = await factory.CreateMemberAsync();
        var payload = new
        {
            direction = "Offer", type = "Good", title = "Vélo à vendre", description = "Vélo de ville en bon état, peu servi.",
            categoryId = ListingHelpers.ElectronicsCategoryId, condition = "Good", city = "Bamako", isDonation = false, price = 25000,
        };

        var response = await alice.Client.PostAsJsonAsync("/api/v1/listings", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Idor_other_member_cannot_read_modify_publish_archive_or_delete_my_draft()
    {
        var alice = await factory.CreateMemberAsync();
        var bob = await factory.CreateMemberAsync();
        var draft = await alice.CreateDraftAsync();

        (await bob.Client.GetAsync($"/api/v1/listings/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _anonymous.GetAsync($"/api/v1/listings/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.Client.PutAsJsonAsync($"/api/v1/listings/{draft.Id}", ListingHelpers.NewListing("Piratage !!"), Json.Options))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.Client.PostAsync($"/api/v1/listings/{draft.Id}/publish", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.Client.PostAsync($"/api/v1/listings/{draft.Id}/archive", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.Client.DeleteAsync($"/api/v1/listings/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.Client.UploadPhotoAsync(draft.Id, ListingHelpers.Png())).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var stillMine = await alice.Client.GetFromJsonAsync<ListingResponse>($"/api/v1/listings/{draft.Id}", Json.Options);
        stillMine!.Title.ShouldBe("Vélo de ville");
        stillMine.Status.ShouldBe(ListingStatus.Draft);
    }

    [Fact]
    public async Task Moderator_can_read_a_draft()
    {
        var alice = await factory.CreateMemberAsync();
        var moderator = await factory.CreateMemberAsync(UserRole.Moderator);
        var draft = await alice.CreateDraftAsync();

        (await moderator.Client.GetAsync($"/api/v1/listings/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_works_for_drafts_only()
    {
        var alice = await factory.CreateMemberAsync();
        var draft = await alice.CreateDraftAsync();
        var published = await alice.CreatePublishedAsync();

        (await alice.Client.DeleteAsync($"/api/v1/listings/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await alice.Client.GetAsync($"/api/v1/listings/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await alice.Client.DeleteAsync($"/api/v1/listings/{published.Id}")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Search_filters_by_type_direction_city_donation_and_paginates()
    {
        var alice = await factory.CreateMemberAsync();
        var city = $"Ville{Guid.NewGuid():N}"[..20];
        await alice.CreatePublishedAsync(ListingHelpers.NewListing("Cours de maths", ListingType.Service, categoryId: ListingHelpers.LessonsCategoryId, city: city));
        await alice.CreatePublishedAsync(ListingHelpers.NewListing("Je cherche un vélo", direction: ListingDirection.Request, city: city));
        await alice.CreatePublishedAsync(ListingHelpers.NewListing("Habits enfant à donner", donation: true, city: city));
        await alice.CreateDraftAsync(ListingHelpers.NewListing("Brouillon invisible", city: city));

        async Task<PagedResult<ListingSummary>> Search(string extra) =>
            (await _anonymous.GetFromJsonAsync<PagedResult<ListingSummary>>($"/api/v1/listings?city={city}{extra}", Json.Options))!;

        (await Search("")).TotalCount.ShouldBe(3);
        (await Search("&type=Service")).Items.Single().Title.ShouldBe("Cours de maths");
        (await Search("&direction=Request")).Items.Single().Title.ShouldBe("Je cherche un vélo");
        (await Search("&donationOnly=true")).Items.Single().Title.ShouldBe("Habits enfant à donner");
        (await Search($"&categoryId={ListingHelpers.LessonsCategoryId}")).TotalCount.ShouldBe(1);
        var page2 = await Search("&page=2&pageSize=2");
        page2.Items.Count.ShouldBe(1);
        page2.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task Search_rejects_invalid_parameters_and_is_safe_against_sql_injection()
    {
        (await _anonymous.GetAsync($"/api/v1/listings?q={new string('x', 101)}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _anonymous.GetAsync("/api/v1/listings?type=Voiture")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var injection = await _anonymous.GetAsync("/api/v1/listings?q=" + Uri.EscapeDataString("' OR 1=1; DROP TABLE listings; --"));
        injection.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await injection.Content.ReadFromJsonAsync<PagedResult<ListingSummary>>(Json.Options))!.Items.ShouldBeEmpty();
        (await _anonymous.GetAsync("/api/v1/listings")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Page_size_is_capped_to_fifty()
    {
        var page = await _anonymous.GetFromJsonAsync<PagedResult<ListingSummary>>("/api/v1/listings?pageSize=10000", Json.Options);

        page!.PageSize.ShouldBe(Paging.MaxPageSize);
    }

    [Fact]
    public async Task Photo_upload_returns_webp_and_follows_listing_visibility()
    {
        var alice = await factory.CreateMemberAsync();
        var bob = await factory.CreateMemberAsync();
        var draft = await alice.CreateDraftAsync();

        var upload = await alice.Client.UploadPhotoAsync(draft.Id, ListingHelpers.Png());
        upload.StatusCode.ShouldBe(HttpStatusCode.Created);
        var photo = (await upload.Content.ReadFromJsonAsync<ListingPhotoResponse>(Json.Options))!;

        var own = await alice.Client.GetAsync(photo.Url);
        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        own.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        (await bob.Client.GetAsync(photo.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await alice.Client.PostAsync($"/api/v1/listings/{draft.Id}/publish", null);
        (await _anonymous.GetAsync(photo.Url)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = (await _anonymous.GetFromJsonAsync<ListingResponse>($"/api/v1/listings/{draft.Id}", Json.Options))!;
        summary.Photos.Single().Url.ShouldBe(photo.Url);
    }

    [Fact]
    public async Task Photo_cache_policy_follows_listing_visibility()
    {
        var alice = await factory.CreateMemberAsync();
        var draft = await alice.CreateDraftAsync();
        var photo = (await (await alice.Client.UploadPhotoAsync(draft.Id, ListingHelpers.Png()))
            .Content.ReadFromJsonAsync<ListingPhotoResponse>(Json.Options))!;

        // Brouillon : visible par l'auteur seulement, jamais mis en cache.
        var draftPhoto = await alice.Client.GetAsync(photo.Url);
        draftPhoto.Headers.CacheControl!.NoStore.ShouldBeTrue();
        draftPhoto.Headers.CacheControl.Public.ShouldBeFalse();

        // Publiée : adresse unique et immuable, cache public d'un jour.
        await alice.Client.PostAsync($"/api/v1/listings/{draft.Id}/publish", null);
        var publicPhoto = await _anonymous.GetAsync(photo.Url);
        publicPhoto.Headers.CacheControl!.Public.ShouldBeTrue();
        publicPhoto.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(1));
        publicPhoto.Headers.CacheControl.NoStore.ShouldBeFalse();
        publicPhoto.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");

        // Le reste de l'API (données JSON) n'est toujours jamais mis en cache.
        (await _anonymous.GetAsync($"/api/v1/listings/{draft.Id}")).Headers.CacheControl!.NoStore.ShouldBeTrue();
        (await alice.Client.GetAsync("/api/v1/users/me")).Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Fake_image_with_image_extension_is_rejected()
    {
        var alice = await factory.CreateMemberAsync();
        var draft = await alice.CreateDraftAsync();

        var response = await alice.Client.UploadPhotoAsync(draft.Id, Encoding.UTF8.GetBytes("pas une image"), "photo.jpg", "image/jpeg");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Extensions["code"]!.ToString().ShouldBe("photo.invalid_format");
    }

    [Fact]
    public async Task Sixth_photo_is_rejected_and_photo_can_be_deleted()
    {
        var alice = await factory.CreateMemberAsync();
        var draft = await alice.CreateDraftAsync();
        ListingPhotoResponse? first = null;
        for (var i = 0; i < Listing.MaxPhotos; i++)
        {
            var ok = await alice.Client.UploadPhotoAsync(draft.Id, ListingHelpers.Png());
            first ??= await ok.Content.ReadFromJsonAsync<ListingPhotoResponse>(Json.Options);
        }

        (await alice.Client.UploadPhotoAsync(draft.Id, ListingHelpers.Png())).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await alice.Client.DeleteAsync($"/api/v1/listings/{draft.Id}/photos/{first!.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await alice.Client.UploadPhotoAsync(draft.Id, ListingHelpers.Png())).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Upload_above_request_size_limit_is_refused()
    {
        var alice = await factory.CreateMemberAsync();
        var draft = await alice.CreateDraftAsync();

        var response = await alice.Client.UploadPhotoAsync(draft.Id, new byte[7 * 1024 * 1024]);

        // Kestrel répond 413 ; le serveur de test en mémoire n'applique pas cette limite,
        // c'est alors le contrôle du service (5 Mo max) qui refuse avec 422. Dans les deux cas : refusé.
        ((int)response.StatusCode).ShouldBeOneOf(400, 413, 422);
        var listing = await alice.Client.GetFromJsonAsync<ListingResponse>($"/api/v1/listings/{draft.Id}", Json.Options);
        listing!.Photos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Categories_are_public_and_seeded()
    {
        var categories = await _anonymous.GetFromJsonAsync<List<CategoryResponse>>("/api/v1/categories", Json.Options);

        categories.ShouldNotBeNull();
        categories.ShouldContain(c => c.Name == "Électronique");
        categories.Count.ShouldBeGreaterThanOrEqualTo(12);
    }

    [Fact]
    public async Task Only_admin_manages_categories_and_inactive_category_cannot_be_used()
    {
        var member = await factory.CreateMemberAsync();
        var admin = await factory.CreateMemberAsync(UserRole.Admin);
        var name = $"Catégorie {Guid.NewGuid():N}"[..25];

        (await member.Client.PostAsJsonAsync("/api/v1/admin/categories", new UpsertCategoryRequest(name))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var created = await admin.Client.PostAsJsonAsync("/api/v1/admin/categories", new UpsertCategoryRequest(name));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var category = (await created.Content.ReadFromJsonAsync<CategoryResponse>(Json.Options))!;
        (await admin.Client.PostAsJsonAsync("/api/v1/admin/categories", new UpsertCategoryRequest(name.ToUpperInvariant())))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await admin.Client.PutAsJsonAsync($"/api/v1/admin/categories/{category.Id}", new UpsertCategoryRequest(name, IsActive: false));

        var response = await member.Client.PostAsJsonAsync("/api/v1/listings", ListingHelpers.NewListing(categoryId: category.Id), Json.Options);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }
}
