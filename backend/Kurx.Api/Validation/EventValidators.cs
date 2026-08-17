using System.Text.Json;
using FluentValidation;
using Kurx.Api.Endpoints;
using Kurx.Infrastructure.Events;

namespace Kurx.Api.Validation;

public class CreateEventBodyValidator : AbstractValidator<CreateEventBody>
{
    public CreateEventBodyValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(2, 200);
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty);
        RuleFor(x => x).Must(x => x.EndsAt > x.StartsAt).WithMessage("EndsAt must be after StartsAt.").WithName("EndsAt");
        // A new event cannot start on a day that has already passed. Only `CreateEventBodyValidator`
        // carries this: `UpdateEventBodyValidator` must not, or an organiser could never correct a typo
        // on an event that has already run, and `EventService.CloneAsync` copies a finished event's
        // dates by design.
        //
        // The floor is the start of the current UTC **day**, not `UtcNow`. Deliberately coarser than the
        // wizard's own rule, which refuses earlier-today against the live clock:
        //   · minute granularity here would false-refuse the honest case — a start typed at 16:35 and
        //     submitted at 16:41, six wizard steps later, is not an attack and must not 400;
        //   · day granularity still refuses every genuinely past date, which is what §6 asks for;
        //   · a FUTURE instant can never fall below it, so this rule has no false refusals at all.
        // UTC rather than the body's `Timezone`: the floor is only ever a day behind local midnight
        // anywhere east of UTC, and resolving an arbitrary IANA id inside a validator would put a third
        // clock into a flow that already reconciles two (D-289).
        RuleFor(x => x.StartsAt).Must(s => s >= DateTime.UtcNow.Date)
            .WithMessage("StartsAt cannot be in the past.");
        RuleFor(x => x.Visibility).Must(s => s is null || Enum.TryParse<Kurx.Domain.Enums.EventVisibility>(s, true, out _))
            .WithMessage("Visibility must be Public, Unlisted, or Private.");
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.Website).Must(w => string.IsNullOrWhiteSpace(w) || Uri.TryCreate(w, UriKind.Absolute, out _))
            .WithMessage("Website must be a valid absolute URL.");
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity is not null);
        RuleForEach(x => x.Tags).Length(1, 40).When(x => x.Tags is not null);
        RuleFor(x => x.SocialLinksJson)
            .Must(EventValidationHelpers.IsValidJson)
            .WithMessage("social_links_json must be valid JSON.")
            .When(x => x.SocialLinksJson is not null);
    }
}

public class UpdateEventBodyValidator : AbstractValidator<UpdateEventBody>
{
    public UpdateEventBodyValidator()
    {
        RuleFor(x => x.Title).Length(2, 200).When(x => x.Title is not null);
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty).When(x => x.CategoryId is not null);
        RuleFor(x => x).Must(x => x.EndsAt is null || x.StartsAt is null || x.EndsAt > x.StartsAt)
            .WithMessage("EndsAt must be after StartsAt.").WithName("EndsAt");
        RuleFor(x => x.Visibility).Must(s => s is null || Enum.TryParse<Kurx.Domain.Enums.EventVisibility>(s, true, out _))
            .WithMessage("Visibility must be Public, Unlisted, or Private.");
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.Website).Must(w => string.IsNullOrWhiteSpace(w) || Uri.TryCreate(w, UriKind.Absolute, out _))
            .WithMessage("Website must be a valid absolute URL.");
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity is not null);
        RuleForEach(x => x.Tags).Length(1, 40).When(x => x.Tags is not null);
        RuleFor(x => x.SocialLinksJson)
            .Must(EventValidationHelpers.IsValidJson)
            .WithMessage("social_links_json must be valid JSON.")
            .When(x => x.SocialLinksJson is not null);
    }
}

public class TransitionEventBodyValidator : AbstractValidator<TransitionEventBody>
{
    public TransitionEventBodyValidator()
    {
        RuleFor(x => x.Action).NotEmpty().Must(EventStatusWorkflow.IsKnownAction)
            .WithMessage("Action must be one of: submit_review, publish, reject, unpublish, close, archive.");
    }
}

internal static class EventValidationHelpers
{
    internal static bool IsValidJson(string? json)
    {
        if (json is null) return true;
        try { JsonDocument.Parse(json); return true; }
        catch (JsonException) { return false; }
    }
}

// D-266 M5. Lengths mirror the column widths so an over-long field fails at the edge with a message
// rather than at the database with a 500. Whether the letterhead is PRESENT is the service's rule
// (`letterhead_required`), because that is a business rule about evidence, not a malformed request.
public class EventAuthorizationBodyValidator : AbstractValidator<EventAuthorizationBody>
{
    public EventAuthorizationBodyValidator()
    {
        RuleFor(x => x.HeadName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.HeadDesignation).NotEmpty().MaximumLength(160);
        RuleFor(x => x.OfficialEmail).NotEmpty().EmailAddress().MaximumLength(255);
        // E.164 shape only. Whether the number is reachable is not a validator question, and the free-mail
        // warning on the email is deliberately NOT here: it is advice, and a validator can only refuse.
        RuleFor(x => x.OfficialPhone).NotEmpty().MaximumLength(20)
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("Phone must be in international format, e.g. +919876543210.");
        RuleFor(x => x.RepresentativeRole).NotEmpty().MaximumLength(60);
        RuleFor(x => x.RepresentativeRoleOther).MaximumLength(80);
        RuleFor(x => x.LetterheadDocumentKey).MaximumLength(400);
        RuleFor(x => x.SignatureDocumentKey).MaximumLength(400);
        RuleFor(x => x.SupportingDocumentKeys)
            .Must(k => k is null || k.Count <= 10).WithMessage("At most 10 supporting documents.");
    }
}

// D-266 M8. A ceiling on size, and that it parses as JSON — nothing about its shape. The payload is the
// wizard's in-progress form: it deliberately contains half-filled steps that would fail real validation,
// and validating it here would refuse exactly the state autosave exists to preserve.
public class EventDraftBodyValidator : AbstractValidator<EventDraftBody>
{
    public EventDraftBodyValidator()
    {
        RuleFor(x => x.PayloadJson).NotEmpty().MaximumLength(200_000)
            .Must(EventValidationHelpers.IsValidJson).WithMessage("Draft payload must be JSON.");
        RuleFor(x => x.StepKey).MaximumLength(40);
    }
}
