using TradX.Domain.Common;
using TradX.Domain.Listings;

namespace TradX.UnitTests.Domain;

/// <summary>Règles d'une annonce : contenu, machine à états, photos.</summary>
public sealed class ListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    internal static ListingContent Content(
        ListingDirection direction = ListingDirection.Offer,
        ListingType type = ListingType.Good,
        bool donation = false) => new(
        direction, type, " Vélo de ville ", " Un vélo en bon état, freins neufs. ", Guid.NewGuid(), ItemCondition.Good,
        " Bamako ", " ", " Le week-end ", " Une perceuse ", donation);

    private static Listing Draft() => Listing.Create(Owner, Content(), Now);

    private static Listing Published()
    {
        var listing = Draft();
        listing.Publish(Now);
        return listing;
    }

    [Fact]
    public void New_listing_is_a_trimmed_draft()
    {
        var listing = Draft();

        listing.Status.ShouldBe(ListingStatus.Draft);
        listing.Title.ShouldBe("Vélo de ville");
        listing.City.ShouldBe("Bamako");
        listing.Neighborhood.ShouldBeNull();
        listing.PublishedAt.ShouldBeNull();
        listing.IsPubliclyVisible.ShouldBeFalse();
    }

    [Fact]
    public void Only_an_offer_can_be_a_donation()
    {
        var ex = Should.Throw<BusinessRuleException>(() =>
            Listing.Create(Owner, Content(ListingDirection.Request, donation: true), Now));

        ex.Code.ShouldBe("listing.donation_must_be_offer");
    }

    [Fact]
    public void Donation_has_no_wanted_counterpart()
    {
        Listing.Create(Owner, Content(donation: true), Now).WantedInReturn.ShouldBeNull();
    }

    [Fact]
    public void Condition_is_ignored_for_services()
    {
        Listing.Create(Owner, Content(type: ListingType.Service), Now).Condition.ShouldBeNull();
    }

    [Fact]
    public void Publishing_a_draft_makes_it_public_and_keeps_first_publication_date()
    {
        var listing = Draft();
        listing.Publish(Now);
        listing.Archive(Now.AddDays(1));

        listing.Publish(Now.AddDays(2));

        listing.Status.ShouldBe(ListingStatus.Published);
        listing.PublishedAt.ShouldBe(Now);
        listing.IsPubliclyVisible.ShouldBeTrue();
    }

    [Fact]
    public void Publishing_twice_is_rejected()
    {
        var listing = Published();

        Should.Throw<BusinessRuleException>(() => listing.Publish(Now)).Code.ShouldBe("listing.invalid_transition");
    }

    [Fact]
    public void Owner_cannot_republish_or_archive_a_suspended_listing()
    {
        var listing = Published();
        listing.Suspend(Now);

        Should.Throw<BusinessRuleException>(() => listing.Publish(Now));
        Should.Throw<BusinessRuleException>(() => listing.Archive(Now));
        Should.Throw<BusinessRuleException>(() => listing.Reserve(Now));
        listing.Status.ShouldBe(ListingStatus.Suspended);
    }

    [Fact]
    public void Exchange_lifecycle_reserve_then_exchanged_is_final()
    {
        var listing = Published();

        listing.Reserve(Now);
        listing.MarkExchanged(Now);

        listing.Status.ShouldBe(ListingStatus.Exchanged);
        Should.Throw<BusinessRuleException>(() => listing.Archive(Now));
        Should.Throw<BusinessRuleException>(() => listing.Suspend(Now));
    }

    [Fact]
    public void Release_puts_a_reserved_listing_back_online()
    {
        var listing = Published();
        listing.Reserve(Now);

        listing.Release(Now);

        listing.Status.ShouldBe(ListingStatus.Published);
    }

    [Fact]
    public void Draft_cannot_be_reserved_or_exchanged()
    {
        var listing = Draft();

        Should.Throw<BusinessRuleException>(() => listing.Reserve(Now));
        Should.Throw<BusinessRuleException>(() => listing.MarkExchanged(Now));
    }

    [Fact]
    public void Suspension_remembers_previous_status_and_reinstate_restores_it()
    {
        var listing = Published();
        listing.Reserve(Now);

        listing.Suspend(Now);
        listing.StatusBeforeSuspension.ShouldBe(ListingStatus.Reserved);
        listing.Reinstate(Now);

        listing.Status.ShouldBe(ListingStatus.Reserved);
        listing.StatusBeforeSuspension.ShouldBeNull();
    }

    [Fact]
    public void Releasing_while_suspended_makes_reinstate_go_back_to_published()
    {
        var listing = Published();
        listing.Reserve(Now);
        listing.Suspend(Now);

        listing.Release(Now);
        listing.Reinstate(Now);

        listing.Status.ShouldBe(ListingStatus.Published);
    }

    [Fact]
    public void Reinstate_requires_a_suspended_listing()
    {
        Should.Throw<BusinessRuleException>(() => Published().Reinstate(Now)).Code.ShouldBe("listing.not_suspended");
    }

    [Fact]
    public void Appeal_is_only_possible_when_suspended()
    {
        var listing = Published();
        Should.Throw<BusinessRuleException>(() => listing.Appeal("Je conteste", Now));

        listing.Suspend(Now);
        listing.Appeal("  Ce bien est toujours disponible.  ", Now);

        listing.SuspensionAppeal.ShouldBe("Ce bien est toujours disponible.");
    }

    [Theory]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Exchanged)]
    [InlineData(ListingStatus.Suspended)]
    public void Content_cannot_be_edited_in_locked_states(ListingStatus status)
    {
        var listing = Published();
        listing.TransitionTo(status == ListingStatus.Exchanged ? ListingStatus.Reserved : status, Now);
        if (status == ListingStatus.Exchanged)
        {
            listing.MarkExchanged(Now);
        }

        Should.Throw<BusinessRuleException>(() => listing.Update(Content(), Now)).Code.ShouldBe("listing.not_editable");
    }

    [Fact]
    public void Up_to_five_photos_with_increasing_positions()
    {
        var listing = Draft();
        for (var i = 0; i < Listing.MaxPhotos; i++)
        {
            listing.AddPhoto($"k{i}", 10, 10, 100, Now);
        }

        listing.Photos.Select(p => p.Position).ShouldBe([0, 1, 2, 3, 4]);
        Should.Throw<BusinessRuleException>(() => listing.AddPhoto("k6", 10, 10, 100, Now)).Code.ShouldBe("listing.too_many_photos");
    }

    [Fact]
    public void Removing_a_photo_frees_a_slot_and_unknown_photo_is_not_found()
    {
        var listing = Draft();
        var photo = listing.AddPhoto("k", 10, 10, 100, Now);

        listing.RemovePhoto(photo.Id, Now).StorageKey.ShouldBe("k");

        listing.Photos.ShouldBeEmpty();
        Should.Throw<NotFoundException>(() => listing.RemovePhoto(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Category_is_created_active_and_can_be_renamed_or_disabled()
    {
        var category = Category.Create(" Informatique ");

        category.Name.ShouldBe("Informatique");
        category.IsActive.ShouldBeTrue();

        category.Rename("Info");
        category.SetActive(false);

        category.Name.ShouldBe("Info");
        category.IsActive.ShouldBeFalse();
    }
}
