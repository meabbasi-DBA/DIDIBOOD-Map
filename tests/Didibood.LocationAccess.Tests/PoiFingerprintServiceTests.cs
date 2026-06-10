using Didibood.LocationAccess.Infrastructure.Services;

namespace Didibood.LocationAccess.Tests;

public class PoiFingerprintServiceTests
{
    private readonly PoiFingerprintService _sut = new();

    [Fact]
    public void ComputeFingerprint_IsDeterministic()
    {
        var a = _sut.ComputeFingerprint("رستوران مجید", "restaurant", 35.691311, 51.388359, "جمهوری اسلامی");
        var b = _sut.ComputeFingerprint("رستوران مجید", "restaurant", 35.691311, 51.388359, "جمهوری اسلامی");
        Assert.Equal(a, b);
    }

    [Fact]
    public void ComputeFingerprint_DifferentAddress_ProducesDifferentHash()
    {
        var a = _sut.ComputeFingerprint("Test", "restaurant", 35.6892, 51.389, "Addr A");
        var b = _sut.ComputeFingerprint("Test", "restaurant", 35.6892, 51.389, "Addr B");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeFingerprint_MatchesPhase3Sample()
    {
        var hash = _sut.ComputeFingerprint(
            "رستوران مجید",
            "restaurant",
            35.69131088256836,
            51.38835906982422,
            "جمهوری اسلامی، سلیمانیه، فرشچی");

        Assert.Equal("27f7f97d75042039db4dffd50d0f1ccad9ffc4877635260099a3f7222247f261", hash);
    }
}
