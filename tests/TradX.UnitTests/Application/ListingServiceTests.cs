using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TradX.Application.Common;
using TradX.Application.Listings;
using TradX.Domain.Common;
using TradX.Domain.Listings;
using TradX.Domain.Users;
using TradX.Infrastructure.Files;
using TradX.Infrastructure.Persistence;
using TradX.UnitTests.TestSupport;

namespace TradX.UnitTests.Application;

/// <summary>Service des annonces : propriété (IDOR), visibilité, e-mail confirmé, recherche, photos.</summary>
public sealed class ListingServiceTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero));
    private readonly AppDbContext _db = TestDb.Create();
    private readonly InMemoryFileStorage _storage = new();
    private readonly Category _category = Category.Create("Électronique");
    private readonly Category _inactive = Category.Create("Ancienne");
    private User _alice = null!;
    private User _bob = null!;
    private User _unconfirmed = null!;
    private User _moderator = null!;

    public async Task InitializeAsync()
    {
        _inactive.SetActive(false);
        _alice = ConfirmedUser("alice@example.ml");
        _bob = ConfirmedUser("bob@example.ml");
        _moderator = ConfirmedUser("modo@example.ml");
        _moderator.ChangeRole(UserRole.Moderator);
        _unconfirmed = User.Register("new@example.ml", "Nouveau", "h", "Bamako", null, _clock.GetUtcNow());
        _db.Users.AddRange(_alice, _bob, _moderator, _unconfirmed);
        _db.Categories.AddRange(_category, _inactive);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private User ConfirmedUser(string email)
    {
        var user = User.Register(email, email.Split('@')[0], "h", "Bamako", null, _clock.GetUtcNow());
        user.IssueEmailConfirmationToken(Guid.NewGuid().ToString(), _clock.GetUtcNow().AddHours(1));
        user.ConfirmEmail(_clock.GetUtcNow());
        return user;
    }

    private ListingService As(User? user)
    {
        var current = CurrentUserFactory.For(user?.Id);
        current.Role.Returns(user?.Role);
        return new ListingService(_db, current, new AuditLogger(_db, current, _clock), _storage, _clock);
    }

    private ListingPhotoService PhotosAs(User? user)
    {
        var current = CurrentUserFactory.For(user?.Id);
        current.Role.Returns(user?.Role);
        return new ListingPhotoService(_db, current, new AuditLogger(_db, current, _clock), new SkiaImageProcessor(), _storage, _clock);
    }

    private UpsertListingRequest Request(string title = "Vélo de ville", Guid? categoryId = null, string city = "Bamako") => new(
        ListingDirection.Offer, ListingType.Good, title, $"{title} : objet en bon état, à échanger.", categoryId ?? _category.Id,
        ItemCondition.Good, city, "Badalabougou", null, "Une perceuse", false);

    private async Task<ListingResponse> PublishedListingOf(User owner, string title = "Vélo de ville", string city = "Bamako")
    {
        var created = await As(owner).CreateAsync(Request(title, city: city), default);
        return await As(owner).PublishAsync(created.Id, default);
    }

    [Fact]
    public async Task Create_makes_a_draft_owned_by_the_connected_member()
    {
        var listing = await As(_alice).CreateAsync(Request(), default);

        listing.Status.ShouldBe(ListingStatus.Draft);
        listing.Owner.Id.ShouldBe(_alice.Id);
        listing.Category.Name.ShouldBe("Électronique");
        _db.AuditLogs.ShouldContain(a => a.Action == "listing.created" && a.ResourceId == listing.Id);
    }

    [Fact]
    public async Task Unconfirmed_member_cannot_create_a_listing()
    {
        var ex = await Should.ThrowAsync<BusinessRuleException>(() => As(_unconfirmed).CreateAsync(Request(), default));

        ex.Code.ShouldBe("user.email_not_confirmed");
    }

    [Fact]
    public async Task Anonymous_cannot_create_a_listing()
    {
        await Should.ThrowAsync<AuthenticationFailedException>(() => As(null).CreateAsync(Request(), default));
    }

    [Fact]
    public async Task Inactive_or_unknown_category_is_rejected()
    {
        await Should.ThrowAsync<BusinessRuleException>(() => As(_alice).CreateAsync(Request(categoryId: _inactive.Id), default));
        await Should.ThrowAsync<BusinessRuleException>(() => As(_alice).CreateAsync(Request(categoryId: Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Draft_is_visible_to_owner_and_moderator_but_not_found_for_others()
    {
        var draft = await As(_alice).CreateAsync(Request(), default);

        (await As(_alice).GetAsync(draft.Id, default)).Id.ShouldBe(draft.Id);
        (await As(_moderator).GetAsync(draft.Id, default)).Id.ShouldBe(draft.Id);
        await Should.ThrowAsync<NotFoundException>(() => As(_bob).GetAsync(draft.Id, default));
        await Should.ThrowAsync<NotFoundException>(() => As(null).GetAsync(draft.Id, default));
    }

    [Fact]
    public async Task Published_listing_is_visible_to_everyone()
    {
        var listing = await PublishedListingOf(_alice);

        (await As(null).GetAsync(listing.Id, default)).Status.ShouldBe(ListingStatus.Published);
    }

    [Fact]
    public async Task Another_member_cannot_update_publish_archive_or_delete_my_listing()
    {
        var mine = await As(_alice).CreateAsync(Request(), default);
        var bob = As(_bob);

        await Should.ThrowAsync<NotFoundException>(() => bob.UpdateAsync(mine.Id, Request("Piraté !!"), default));
        await Should.ThrowAsync<NotFoundException>(() => bob.PublishAsync(mine.Id, default));
        await Should.ThrowAsync<NotFoundException>(() => bob.ArchiveAsync(mine.Id, default));
        await Should.ThrowAsync<NotFoundException>(() => bob.DeleteAsync(mine.Id, default));
        (await As(_alice).GetAsync(mine.Id, default)).Title.ShouldBe("Vélo de ville");
    }

    [Fact]
    public async Task Update_changes_content_and_is_audited()
    {
        var listing = await As(_alice).CreateAsync(Request(), default);

        var updated = await As(_alice).UpdateAsync(listing.Id, Request("Vélo tout terrain"), default);

        updated.Title.ShouldBe("Vélo tout terrain");
        _db.AuditLogs.ShouldContain(a => a.Action == "listing.updated");
    }

    [Fact]
    public async Task Only_drafts_can_be_deleted_and_their_photos_are_removed_from_storage()
    {
        var draft = await As(_alice).CreateAsync(Request(), default);
        await PhotosAs(_alice).UploadAsync(draft.Id, new MemoryStream(TestImages.Png()), TestImages.Png().Length, default);
        var published = await PublishedListingOf(_alice);

        await As(_alice).DeleteAsync(draft.Id, default);

        _db.Listings.Any(l => l.Id == draft.Id).ShouldBeFalse();
        _storage.Files.ShouldBeEmpty();
        var ex = await Should.ThrowAsync<BusinessRuleException>(() => As(_alice).DeleteAsync(published.Id, default));
        ex.Code.ShouldBe("listing.only_drafts_deletable");
    }

    [Fact]
    public async Task Search_returns_only_published_listings_with_filters_and_pagination()
    {
        await PublishedListingOf(_alice, "Vélo rouge");
        await PublishedListingOf(_bob, "Vélo bleu", "Kati");
        await PublishedListingOf(_bob, "Table en bois");
        await As(_alice).CreateAsync(Request("Vélo brouillon"), default);

        var velos = await As(null).SearchAsync(new SearchListingsQuery(Q: "VÉLO"), default);
        var inKati = await As(null).SearchAsync(new SearchListingsQuery(City: "kati"), default);
        var page = await As(null).SearchAsync(new SearchListingsQuery(Page: 2, PageSize: 2), default);

        velos.Items.Select(i => i.Title).ShouldBe(["Vélo bleu", "Vélo rouge"], ignoreOrder: true);
        inKati.Items.Single().Title.ShouldBe("Vélo bleu");
        page.TotalCount.ShouldBe(3);
        page.Items.Count.ShouldBe(1);
        page.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task Mine_lists_all_my_listings_whatever_their_status()
    {
        await As(_alice).CreateAsync(Request("Brouillon à moi"), default);
        await PublishedListingOf(_alice);
        await PublishedListingOf(_bob);

        var mine = await As(_alice).MineAsync(null, null, default);

        mine.TotalCount.ShouldBe(2);
        mine.Items.ShouldAllBe(i => i.Owner.Id == _alice.Id);
    }

    [Fact]
    public async Task Photo_upload_stores_clean_webp_and_is_limited_to_owner()
    {
        var listing = await As(_alice).CreateAsync(Request(), default);
        var jpeg = TestImages.JpegWithGpsMetadata();

        var photo = await PhotosAs(_alice).UploadAsync(listing.Id, new MemoryStream(jpeg), jpeg.Length, default);

        photo.Url.ShouldBe($"/api/v1/listings/{listing.Id}/photos/{photo.Id}");
        var stored = _storage.Files.Single().Value;
        System.Text.Encoding.ASCII.GetString(stored).ShouldNotContain(TestImages.GpsMarker);
        await Should.ThrowAsync<NotFoundException>(() =>
            PhotosAs(_bob).UploadAsync(listing.Id, new MemoryStream(jpeg), jpeg.Length, default));
    }

    [Fact]
    public async Task Photo_of_a_draft_is_hidden_from_others()
    {
        var listing = await As(_alice).CreateAsync(Request(), default);
        var png = TestImages.Png();
        var photo = await PhotosAs(_alice).UploadAsync(listing.Id, new MemoryStream(png), png.Length, default);

        await using (await PhotosAs(_alice).OpenAsync(listing.Id, photo.Id, default))
        {
        }

        await Should.ThrowAsync<NotFoundException>(() => PhotosAs(_bob).OpenAsync(listing.Id, photo.Id, default));
        await Should.ThrowAsync<NotFoundException>(() => PhotosAs(null).OpenAsync(listing.Id, photo.Id, default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5 * 1024 * 1024 + 1)]
    public async Task Photo_size_must_be_between_1_byte_and_5_megabytes(long length)
    {
        var listing = await As(_alice).CreateAsync(Request(), default);

        var ex = await Should.ThrowAsync<BusinessRuleException>(() =>
            PhotosAs(_alice).UploadAsync(listing.Id, new MemoryStream([1]), length, default));

        ex.Code.ShouldBe("photo.invalid_size");
    }

    [Fact]
    public async Task Deleting_a_photo_removes_the_file()
    {
        var listing = await As(_alice).CreateAsync(Request(), default);
        var png = TestImages.Png();
        var photo = await PhotosAs(_alice).UploadAsync(listing.Id, new MemoryStream(png), png.Length, default);

        await PhotosAs(_alice).DeleteAsync(listing.Id, photo.Id, default);

        _storage.Files.ShouldBeEmpty();
    }
}

/// <summary>Stockage de fichiers en mémoire pour les tests.</summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public Task SaveAsync(string key, byte[] content, CancellationToken ct)
    {
        Files[key] = content;
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out var data) ? new MemoryStream(data) : null);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }
}
