using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

// Deliberately thin (D-262). Body length and emptiness are NOT validated here even though they could
// be: the frozen client contract answers those with `body_required` / `body_too_long`, which the UI
// switches on, and a validator would pre-empt them with a generic `validation_failed` the UI cannot
// act on. What is left is shape a client can only get wrong by being broken.
//
// Everything depending on state the request cannot see — whether the media is this caller's, whether
// the event admits them, whether the poll is open — belongs in IPostService, because a validator has
// no database and an authorization check at the edge is one that can disagree with the real one.

public class CreatePostBodyValidator : AbstractValidator<CreatePostBody>
{
    public CreatePostBodyValidator()
    {
        // A ceiling on the number of ids, not on which ids: the service checks ownership, confirmation
        // and the per-kind caps, and answers `too_many_media` / `invalid_media` for those.
        RuleFor(x => x.MediaIds).Must(ids => ids is null || ids.Count <= 16)
            .WithMessage("A post carries at most 16 files.");
        When(x => x.Poll is not null, () =>
        {
            RuleFor(x => x.Poll!.Question).MaximumLength(300);
            RuleForEach(x => x.Poll!.Options).MaximumLength(120);
            RuleFor(x => x.Poll!.Options).Must(o => o is null || o.Count <= 10)
                .WithMessage("A poll has at most 10 options.");
        });
    }
}

public class UpdatePostBodyValidator : AbstractValidator<UpdatePostBody>
{
    // A PATCH that names nothing would otherwise answer 200 while changing only EditedAt — a client
    // bug the server would be hiding.
    public UpdatePostBodyValidator() =>
        RuleFor(x => x).Must(x => x.Body is not null || x.Visibility is not null)
            .WithMessage("Supply body, visibility, or both.");
}

public class PresignPostMediaBodyValidator : AbstractValidator<PresignPostMediaBody>
{
    public PresignPostMediaBodyValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(150);
    }
}

public class HidePostBodyValidator : AbstractValidator<HidePostBody>
{
    // A stored moderation reason that may be blank is a field that means nothing — the rule D-193
    // applied to event hide/suspend, for the same reason.
    public HidePostBodyValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}
