using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

public class ApplyReferralBodyValidator : AbstractValidator<ApplyReferralBody>
{
    public ApplyReferralBodyValidator()
    {
        RuleFor(x => x.ReferralCode).NotEmpty().WithMessage("Referral code is required.");
    }
}
