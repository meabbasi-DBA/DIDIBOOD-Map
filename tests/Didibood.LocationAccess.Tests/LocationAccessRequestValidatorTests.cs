using Didibood.LocationAccess.Application.LocationAccess;

namespace Didibood.LocationAccess.Tests;

public class LocationAccessRequestValidatorTests
{
    private readonly LocationAccessRequestValidator _validator = new();

    [Theory]
    [InlineData(35.7, 51.4, 2000)]
    [InlineData(35.7, 51.4, 100)]
    [InlineData(35.7, 51.4, 10000)]
    public void ValidRequest_Passes(double lat, double lng, int radius)
    {
        var result = _validator.Validate(new LocationAccessRequest
        {
            Latitude = lat,
            Longitude = lng,
            Radius = radius
        });
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(91, 51.4, 2000)]
    [InlineData(35.7, 181, 2000)]
    [InlineData(35.7, 51.4, 50)]
    public void InvalidRequest_Fails(double lat, double lng, int radius)
    {
        var result = _validator.Validate(new LocationAccessRequest
        {
            Latitude = lat,
            Longitude = lng,
            Radius = radius
        });
        Assert.False(result.IsValid);
    }
}
