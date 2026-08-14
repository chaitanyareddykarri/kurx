using FluentValidation;
using Kurx.Api.Endpoints;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;

namespace Kurx.Api.Validation;

public class CreateOrderInputValidator : AbstractValidator<CreateOrderInput>
{
    public CreateOrderInputValidator()
    {
        RuleFor(x => x.TicketTypeId).NotEmpty();
        RuleFor(x => x.GroupSize).GreaterThan(0).When(x => x.GroupSize is not null);
        // Requiredness of guest fields depends on auth state (unknown to FluentValidation) — only
        // format is checked here; OrderService enforces guest_contact_required when applicable.
        RuleFor(x => x.GuestPhone).Length(8, 16).When(x => x.GuestPhone is not null);
        RuleFor(x => x.GuestEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.GuestEmail));
    }
}

public class JoinGroupInputValidator : AbstractValidator<JoinGroupInput>
{
    public JoinGroupInputValidator()
    {
        RuleFor(x => x.JoinCode).NotEmpty().Length(4, 10);
    }
}

public class SpeakerBodyValidator : AbstractValidator<SpeakerBody>
{
    public SpeakerBodyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 150);
    }
}

public class SponsorBodyValidator : AbstractValidator<SponsorBody>
{
    public SponsorBodyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 150);
        RuleFor(x => x.Tier).Must(t => t is null || Enum.TryParse<SponsorTier>(t, true, out _))
            .WithMessage("Tier must be one of Platinum, Gold, Silver, Bronze, Partner.");
        RuleFor(x => x.Website).Must(w => string.IsNullOrWhiteSpace(w) || Uri.TryCreate(w, UriKind.Absolute, out _))
            .WithMessage("Website must be a valid absolute URL.");
    }
}

public class SessionBodyValidator : AbstractValidator<SessionBody>
{
    public SessionBodyValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(1, 200);
        RuleFor(x => x.Kind).Must(k => k is null || Enum.TryParse<ScheduleItemKind>(k, true, out _))
            .WithMessage("Kind must be Session or Break.");
        RuleFor(x => x).Must(x => x.EndsAt > x.StartsAt).WithMessage("EndsAt must be after StartsAt.").WithName("EndsAt");
    }
}

public class PresignMediaBodyValidator : AbstractValidator<PresignMediaBody>
{
    public PresignMediaBodyValidator()
    {
        RuleFor(x => x.ContentType).NotEmpty();
        RuleFor(x => x.MaxBytes).GreaterThan(0);
    }
}

public class AttachMediaBodyValidator : AbstractValidator<AttachMediaBody>
{
    public AttachMediaBodyValidator()
    {
        RuleFor(x => x.Kind).NotEmpty().IsEnumName(typeof(MediaKind), caseSensitive: false);
        RuleFor(x => x.Key).NotEmpty();
    }
}

public class CreateTemplateBodyValidator : AbstractValidator<CreateTemplateBody>
{
    public CreateTemplateBodyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
    }
}

public class TicketTypeBodyValidator : AbstractValidator<TicketTypeBody>
{
    private static readonly string[] ValidPricingUnits = Enum.GetNames<PricingUnit>().Select(n => n.ToLower()).ToArray();
    private static readonly string[] ValidRegModes = Enum.GetNames<RegistrationMode>().Select(n => n.ToLower()).ToArray();

    public TicketTypeBodyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(1, 150);
        RuleFor(x => x.PricePaise).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PricingUnit).Must(u => Enum.TryParse<PricingUnit>(u, true, out _))
            .WithMessage($"PricingUnit must be one of: {string.Join(", ", ValidPricingUnits)}.");
        RuleFor(x => x.RegistrationMode).Must(m => Enum.TryParse<RegistrationMode>(m, true, out _))
            .WithMessage($"RegistrationMode must be one of: {string.Join(", ", ValidRegModes)}.");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x).Must(x => x.SaleEnds > x.SaleStarts).WithMessage("SaleEnds must be after SaleStarts.").WithName("SaleEnds");
        RuleFor(x => x.PerUserLimit).GreaterThanOrEqualTo(1);
        RuleFor(x => x).Must(x =>
            !string.Equals(x.RegistrationMode, "Group", StringComparison.OrdinalIgnoreCase) ||
            (x.GroupMin is not null && x.GroupMax is not null && x.GroupMin <= x.GroupMax))
            .WithMessage("Group registration requires GroupMin and GroupMax (min <= max).")
            .WithName("GroupMin");
    }
}

public class FormFieldBodyValidator : AbstractValidator<FormFieldBody>
{
    public FormFieldBodyValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Length(1, 100)
            .Matches(@"^[a-z][a-z0-9_]*$").WithMessage("Key must be snake_case (lowercase letters, digits, underscore; must start with a letter).");
        RuleFor(x => x.Label).NotEmpty().Length(1, 200);
        RuleFor(x => x.Type).Must(t => Enum.TryParse<FormFieldType>(t, true, out _))
            .WithMessage("Type must be one of: Text, Number, Select, Checkbox, Date, File.");
        RuleFor(x => x.Scope).Must(s => Enum.TryParse<FormFieldScope>(s, true, out _))
            .WithMessage("Scope must be PerRegistration or PerParticipant.");
    }
}
