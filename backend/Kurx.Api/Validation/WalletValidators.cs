using FluentValidation;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Validation;

public class WithdrawInputValidator : AbstractValidator<WithdrawInput>
{
    public WithdrawInputValidator()
    {
        RuleFor(x => x.AmountPaise)
            .GreaterThan(0)
            .WithMessage("amount_paise must be greater than zero.");
    }
}
