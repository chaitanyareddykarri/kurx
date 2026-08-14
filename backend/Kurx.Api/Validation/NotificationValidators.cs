using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

public class RegisterDeviceBodyValidator : AbstractValidator<RegisterDeviceBody>
{
    public RegisterDeviceBodyValidator()
    {
        RuleFor(x => x.FcmToken).NotEmpty().WithMessage("FcmToken is required.");
        RuleFor(x => x.Platform).NotEmpty().Must(p => p.ToLower() == "android" || p.ToLower() == "ios")
            .WithMessage("Platform must be 'android' or 'ios'.");
    }
}

public class UpdateDeviceBodyValidator : AbstractValidator<UpdateDeviceBody>
{
    public UpdateDeviceBodyValidator()
    {
        RuleFor(x => x.DeviceName).MaximumLength(200).WithMessage("Device name must not exceed 200 characters.");
    }
}
