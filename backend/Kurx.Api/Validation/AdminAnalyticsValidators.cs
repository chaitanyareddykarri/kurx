using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

public class BroadcastNotificationBodyValidator : AbstractValidator<BroadcastNotificationBody>
{
    public BroadcastNotificationBodyValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Title is required and must not exceed 200 characters.");
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000).WithMessage("Message is required and must not exceed 2000 characters.");
    }
}
