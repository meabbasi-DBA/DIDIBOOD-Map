using Didibood.LocationAccess.Application.Abstractions;
using FluentValidation;

namespace Didibood.LocationAccess.Application.StaticMap;

public sealed class StaticMapRequestValidator : AbstractValidator<StaticMapRequest>
{
    public StaticMapRequestValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Zoom).InclusiveBetween(5, 19);
        RuleFor(x => x.Width).InclusiveBetween(1, 2048);
        RuleFor(x => x.Height).InclusiveBetween(1, 2048);
        RuleFor(x => x.Style)
            .NotEmpty()
            .Must(s => s == "light" || s == "dark")
            .WithMessage("Style must be 'light' or 'dark'.");
    }
}
