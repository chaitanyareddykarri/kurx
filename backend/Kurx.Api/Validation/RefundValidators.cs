using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

// D-202 (D-201 review finding): Reason is a permanent financial-audit-trail field with no DB-level
// length cap (Refund.Reason has no HasMaxLength) — matches ModerationActionBodyValidator's identical shape.
public class RequestRefundBodyValidator : AbstractValidator<RequestRefundBody>
{
    public RequestRefundBodyValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
