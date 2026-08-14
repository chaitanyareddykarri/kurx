using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

public class VenueBodyValidator : AbstractValidator<VenueBody>
{
    public VenueBodyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 150);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity is not null);
        RuleFor(x => x.GoogleMapsUrl).Must(u => string.IsNullOrWhiteSpace(u) || Uri.TryCreate(u, UriKind.Absolute, out _))
            .WithMessage("GoogleMapsUrl must be a valid absolute URL.");
    }
}
