using System.Text;
using Microsoft.Extensions.Options;
using TradX.Application.Listings;
using TradX.Domain.Common;
using TradX.Domain.Listings;
using TradX.Infrastructure.Files;
using TradX.UnitTests.TestSupport;

namespace TradX.UnitTests.Infrastructure;

/// <summary>Traitement d'images (sécurité des uploads) et stockage local (path traversal).</summary>
public sealed class FilesTests
{
    private readonly SkiaImageProcessor _processor = new();

    [Fact]
    public void Png_is_converted_to_webp()
    {
        var result = _processor.Process(TestImages.Png(64, 48));

        SkiaImageProcessor.HasAllowedSignature(result.Data).ShouldBeTrue();
        Encoding.ASCII.GetString(result.Data, 8, 4).ShouldBe("WEBP");
        (result.Width, result.Height).ShouldBe((64, 48));
    }

    [Fact]
    public void Exif_and_gps_metadata_are_removed()
    {
        var input = TestImages.JpegWithGpsMetadata();
        Encoding.ASCII.GetString(input).ShouldContain(TestImages.GpsMarker);

        var result = _processor.Process(input);

        var output = Encoding.ASCII.GetString(result.Data);
        output.ShouldNotContain(TestImages.GpsMarker);
        output.ShouldNotContain("Exif");
    }

    [Fact]
    public void Large_images_are_downscaled_keeping_ratio()
    {
        var result = _processor.Process(TestImages.Png(2560, 1280));

        (result.Width, result.Height).ShouldBe((SkiaImageProcessor.MaxDimension, 640));
    }

    [Theory]
    [InlineData("Ceci n'est pas une image")]
    [InlineData("%PDF-1.7 faux document")]
    [InlineData("<html><body>page</body></html>")]
    [InlineData("GIF89a")]
    public void Non_image_content_is_rejected_whatever_its_name(string content)
    {
        var ex = Should.Throw<BusinessRuleException>(() => _processor.Process(Encoding.UTF8.GetBytes(content)));

        ex.Code.ShouldBe("photo.invalid_format");
    }

    [Fact]
    public void Truncated_image_with_valid_signature_is_rejected()
    {
        var truncated = TestImages.Png()[..20];

        Should.Throw<BusinessRuleException>(() => _processor.Process(truncated));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, true)]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, false)]
    [InlineData(new byte[] { }, false)]
    public void Signature_check_accepts_only_jpeg_png_webp(byte[] header, bool expected)
    {
        SkiaImageProcessor.HasAllowedSignature(header).ShouldBe(expected);
    }

    [Fact]
    public async Task Local_storage_round_trip_and_delete()
    {
        var root = Path.Combine(Path.GetTempPath(), "tradx-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalFileStorage(Options.Create(new StorageOptions { RootPath = root }));

            await storage.SaveAsync("listings/a/b.webp", [1, 2, 3], default);
            await using (var stream = await storage.OpenReadAsync("listings/a/b.webp", default))
            {
                stream.ShouldNotBeNull().Length.ShouldBe(3);
            }

            await storage.DeleteAsync("listings/a/b.webp", default);
            (await storage.OpenReadAsync("listings/a/b.webp", default)).ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("listings/../../etc/passwd")]
    [InlineData("..\\..\\windows\\win.ini")]
    [InlineData("listings\\photo.webp")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/windows/win.ini")]
    [InlineData("")]
    public void Local_storage_rejects_path_traversal(string key)
    {
        var storage = new LocalFileStorage(Options.Create(new StorageOptions { RootPath = Path.GetTempPath() + "tradx-root" }));

        Should.Throw<ArgumentException>(() => storage.Resolve(key));
    }

    [Fact]
    public void Listing_validator_enforces_lengths_and_donation_rule()
    {
        var validator = new UpsertListingRequestValidator();
        var valid = new UpsertListingRequest(ListingDirection.Offer, ListingType.Good, "Vélo de ville",
            "Un vélo en bon état, freins neufs.", Guid.NewGuid(), ItemCondition.Good, "Bamako", null, null, null, false);

        validator.Validate(valid).IsValid.ShouldBeTrue();
        validator.Validate(valid with { Title = "Vél" }).IsValid.ShouldBeFalse();
        validator.Validate(valid with { Description = "trop court" }).IsValid.ShouldBeFalse();
        validator.Validate(valid with { CategoryId = Guid.Empty }).IsValid.ShouldBeFalse();
        validator.Validate(valid with { Condition = null }).IsValid.ShouldBeFalse();
        validator.Validate(valid with { Type = ListingType.Service, Condition = null }).IsValid.ShouldBeTrue();
        validator.Validate(valid with { Direction = ListingDirection.Request, IsDonation = true }).IsValid.ShouldBeFalse();
        validator.Validate(valid with { WantedInReturn = new string('w', 501) }).IsValid.ShouldBeFalse();
        validator.Validate(valid with { Direction = (ListingDirection)9 }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Search_validator_limits_keyword_length()
    {
        var validator = new SearchListingsQueryValidator();

        validator.Validate(new SearchListingsQuery(Q: new string('q', 100))).IsValid.ShouldBeTrue();
        validator.Validate(new SearchListingsQuery(Q: new string('q', 101))).IsValid.ShouldBeFalse();
    }
}
