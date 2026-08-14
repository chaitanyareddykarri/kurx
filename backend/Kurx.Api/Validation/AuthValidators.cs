using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

public class OtpRequestBodyValidator : AbstractValidator<OtpRequestBody>
{
    public OtpRequestBodyValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^[0-9+\-\s()]{7,20}$")
            .WithMessage("Phone must be 7-20 characters of digits, spaces, or +()-.");
    }
}

public class OtpVerifyBodyValidator : AbstractValidator<OtpVerifyBody>
{
    public OtpVerifyBodyValidator()
    {
        RuleFor(x => x.Phone).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("Code must be 6 digits.");
    }
}

public class RefreshBodyValidator : AbstractValidator<RefreshBody>
{
    public RefreshBodyValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class PhoneChangeBodyValidator : AbstractValidator<PhoneChangeBody>
{
    public PhoneChangeBodyValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^[0-9+\-\s()]{7,20}$")
            .WithMessage("Phone must be 7-20 characters of digits, spaces, or +()-.");
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("Code must be 6 digits.");
    }
}
