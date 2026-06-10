using FluentValidation;

namespace Didibood.LocationAccess.Application.LocationAccess;

public sealed class LocationAccessRequestValidator : AbstractValidator<LocationAccessRequest>
{
    public LocationAccessRequestValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Radius).InclusiveBetween(100, 10_000);
    }
}
