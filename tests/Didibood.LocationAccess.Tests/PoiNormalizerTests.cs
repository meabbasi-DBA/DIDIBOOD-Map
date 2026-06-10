using Didibood.LocationAccess.Application.Neshan;
using Didibood.LocationAccess.Infrastructure.Crawler;
using Didibood.LocationAccess.Infrastructure.Services;

namespace Didibood.LocationAccess.Tests;

public class PoiNormalizerTests
{
    private readonly PoiNormalizer _sut = new(new PoiFingerprintService());

    // ── Category filter ──────────────────────────────────────────────────────

    [Fact]
    public void Normalize_WhenNeshanCategoryIsMunicipal_ReturnsNull()
    {
        var item = MakeItem(category: "municipal", type: "hospital");
        Assert.Null(_sut.Normalize(item, categoryId: 6));
    }

    [Fact]
    public void Normalize_WhenNeshanCategoryIsRegion_ReturnsNull()
    {
        var item = MakeItem(category: "region", type: "hospital");
        Assert.Null(_sut.Normalize(item, categoryId: 6));
    }

    [Fact]
    public void Normalize_WhenNeshanCategoryIsNull_ReturnsNull()
    {
        var item = MakeItem(category: null, type: "hospital");
        Assert.Null(_sut.Normalize(item, categoryId: 6));
    }

    // ── Type whitelist ───────────────────────────────────────────────────────

    [Fact]
    public void Normalize_WhenTypeNotInWhitelist_ReturnsNull()
    {
        // hospital category (id=6) should reject "restaurant" type
        var item = MakeItem(category: "place", type: "restaurant");
        Assert.Null(_sut.Normalize(item, categoryId: 6));
    }

    [Fact]
    public void Normalize_WhenTypeIsNull_ReturnsNull()
    {
        var item = MakeItem(category: "place", type: null);
        Assert.Null(_sut.Normalize(item, categoryId: 6));
    }

    [Theory]
    [InlineData(1,  "subway_station")]
    [InlineData(1,  "metro_entrance")]
    [InlineData(1,  "train_station")]
    [InlineData(2,  "bus_station")]
    [InlineData(2,  "transit_station")]
    [InlineData(3,  "bus_station")]
    [InlineData(4,  "formal_school")]
    [InlineData(4,  "school")]
    [InlineData(4,  "tertiary")]
    [InlineData(5,  "university")]
    [InlineData(5,  "college")]
    [InlineData(6,  "hospital")]
    [InlineData(7,  "clinic")]
    [InlineData(8,  "pharmacy")]
    [InlineData(9,  "shopping_mall")]
    [InlineData(9,  "commercial_complex")]
    [InlineData(10, "supermarket")]
    [InlineData(11, "park")]
    [InlineData(12, "gym")]
    [InlineData(13, "bank")]
    [InlineData(14, "mosque")]
    [InlineData(15, "local_government_office")]
    [InlineData(15, "e_government")]
    public void Normalize_WhitelistedTypesPerCategory_ReturnsNonNull(short categoryId, string type)
    {
        var item = MakeItem(category: "place", type: type);
        var result = _sut.Normalize(item, categoryId);
        Assert.NotNull(result);
    }

    // ── Field mapping ────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_ValidHospital_MapsFieldsCorrectly()
    {
        var item = new NeshanSearchItem
        {
            Title = "بیمارستان میلاد",
            Address = "ستارخان، تهران",
            Category = "place",
            Type = "hospital",
            Location = new NeshanLocation { X = 51.39, Y = 35.69 },
        };

        var result = _sut.Normalize(item, categoryId: 6);

        Assert.NotNull(result);
        Assert.Equal(6, result.CategoryId);
        Assert.Equal("hospital", result.NeshanType);
        Assert.Equal("place", result.NeshanCategory);
        Assert.Equal(35.69, result.Latitude);
        Assert.Equal(51.39, result.Longitude);
        Assert.Equal("بیمارستان میلاد", result.Title);
        Assert.Equal("ستارخان، تهران", result.Address);
    }

    // ── Fingerprint ──────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_ComputesFingerprintOf64HexChars()
    {
        var item = MakeItem(category: "place", type: "subway_station", title: "مترو تجریش");
        var result = _sut.Normalize(item, categoryId: 1);

        Assert.NotNull(result);
        Assert.Equal(64, result.Fingerprint.Length);
        Assert.Matches("^[0-9a-f]{64}$", result.Fingerprint);
    }

    [Fact]
    public void Normalize_SameInputProducesSameFingerprint()
    {
        var item = MakeItem(category: "place", type: "bank", title: "بانک ملی");
        var r1 = _sut.Normalize(item, categoryId: 13);
        var r2 = _sut.Normalize(item, categoryId: 13);

        Assert.Equal(r1!.Fingerprint, r2!.Fingerprint);
    }

    [Fact]
    public void Normalize_DifferentTitle_ProducesDifferentFingerprint()
    {
        var a = _sut.Normalize(MakeItem(category: "place", type: "bank", title: "بانک ملی"), categoryId: 13);
        var b = _sut.Normalize(MakeItem(category: "place", type: "bank", title: "بانک صادرات"), categoryId: 13);

        Assert.NotEqual(a!.Fingerprint, b!.Fingerprint);
    }

    // ── SourcePayloadJson ────────────────────────────────────────────────────

    [Fact]
    public void Normalize_SourcePayloadJson_IsNotEmpty()
    {
        var item = MakeItem(category: "place", type: "mosque");
        var result = _sut.Normalize(item, categoryId: 14);

        Assert.NotNull(result);
        Assert.NotEmpty(result.SourcePayloadJson);
        Assert.NotEqual("{}", result.SourcePayloadJson);
    }

    // ── Unknown category ID ──────────────────────────────────────────────────

    [Fact]
    public void Normalize_UnknownCategoryId_ReturnsNull()
    {
        var item = MakeItem(category: "place", type: "hospital");
        Assert.Null(_sut.Normalize(item, categoryId: 99));
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static NeshanSearchItem MakeItem(
        string? category,
        string? type,
        string title = "POI",
        string? address = null)
        => new()
        {
            Title = title,
            Address = address,
            Category = category,
            Type = type,
            Location = new NeshanLocation { X = 51.39, Y = 35.69 },
        };
}
