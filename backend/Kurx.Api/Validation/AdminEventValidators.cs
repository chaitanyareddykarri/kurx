using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

public class OrganizerMessageBodyValidator : AbstractValidator<OrganizerMessageBody>
{
    public OrganizerMessageBodyValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
    }
}

// D-193: suspend/hide are the two moderation overrides with a real, stored, audited reason field
// (EventService.SuspendAsync/HideAsync) — a required reason here is what actually makes that field
// mean something. Reason stays required for BOTH single-event routes below since they share this
// one validator (suspend and hide).
public class ModerationActionBodyValidator : AbstractValidator<ModerationActionBody>
{
    public ModerationActionBodyValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

// D-193: bulk mirrors the single-event policy exactly — reason required only for suspend/hide (the
// two actions that store one), not for unsuspend/unhide (no reason field exists for an "undo") or
// approve/reject/archive (those route through TransitionAsync, which has no reason field at all —
// adding one is a separate, larger decision than this validation fix, not made here).
public class BulkEventActionBodyValidator : AbstractValidator<BulkEventActionBody>
{
    public BulkEventActionBodyValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).When(x => x.Action is "suspend" or "hide");
    }
}

// D-266 M5. Shape only: whether a decision is legal for the row's CURRENT state, and whether a reason
// code is required for it, are the service's calls — a validator has no database and would answer them
// from a different set of facts than the one that actually decides.
public class ReviewEventAuthorizationBodyValidator : AbstractValidator<ReviewEventAuthorizationBody>
{
    public ReviewEventAuthorizationBodyValidator()
    {
        RuleFor(x => x.Decision).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ReasonCode).MaximumLength(40);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

// D-266 M7. Shape only — whether the key is on THIS event's checklist is a database question the service
// answers with `unknown_checklist_item`, and a validator with no database would answer it from nothing.
public class ReviewChecklistItemBodyValidator : AbstractValidator<ReviewChecklistItemBody>
{
    public ReviewChecklistItemBodyValidator() => RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(60);
}

// D-266 M7. Whether notes are REQUIRED depends on the verdict, which the service decides — a validator
// asserting it here would be a second copy of the same rule.
public class FinancialReviewBodyValidator : AbstractValidator<FinancialReviewBody>
{
    public FinancialReviewBodyValidator() => RuleFor(x => x.Notes).MaximumLength(2000);
}
