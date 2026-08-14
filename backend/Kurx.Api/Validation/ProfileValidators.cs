using FluentValidation;
using Kurx.Api.Endpoints;

namespace Kurx.Api.Validation;

/// <summary>Bounds on the self-declared profile fields.
///
/// <para><c>PATCH /v1/me/profile</c> ran <b>no validator at all</b>: `name`, `headline` and `bio` were
/// each unbounded, so an authenticated caller could store a field of any size. Every client capped these
/// in its own form — mobile's onboarding at 300 characters, others not at all — which is a UI hint, not
/// a constraint. The same shape as the password policy that read 8 on the client and 12 on the server:
/// a rule enforced only where it is convenient to enforce is not a rule (D-311).</para>
///
/// <para>Limits mirror the organisation profile (<see cref="UpdateOrgBodyValidator"/>) so the two halves
/// of the platform's "profile" concept do not disagree. Deliberately generous: this exists to bound the
/// column, not to have an opinion about how much someone writes about themselves. Username format and
/// uniqueness stay in the endpoint — they need the database, and the reserved-word list already lives
/// there.</para></summary>
public class UpdateProfileBodyValidator : AbstractValidator<UpdateProfileBody>
{
    public UpdateProfileBodyValidator()
    {
        // `When(... is not null)` throughout: every field is partial-update, and absent means unchanged.
        // A bare NotEmpty would reject the omission itself rather than the value.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120).When(x => x.Name is not null);
        RuleFor(x => x.Headline).MaximumLength(200);
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.EducationJson).MaximumLength(4000);
        RuleFor(x => x.LinksJson).MaximumLength(4000);
        RuleFor(x => x.AvatarKey).MaximumLength(512);
        RuleFor(x => x.CoverKey).MaximumLength(512);

        // Collections are bounded by count AND by element, because either one alone still admits an
        // arbitrarily large body.
        RuleFor(x => x.Skills!).Must(v => v.Length <= 50).When(x => x.Skills is not null)
            .WithMessage("At most 50 skills.");
        RuleForEach(x => x.Skills).MaximumLength(60).When(x => x.Skills is not null);
        RuleFor(x => x.Languages!).Must(v => v.Length <= 25).When(x => x.Languages is not null)
            .WithMessage("At most 25 languages.");
        RuleForEach(x => x.Languages).MaximumLength(60).When(x => x.Languages is not null);
        RuleFor(x => x.Interests!).Must(v => v.Length <= 50).When(x => x.Interests is not null)
            .WithMessage("At most 50 interests.");
        RuleForEach(x => x.Interests).MaximumLength(60).When(x => x.Interests is not null);
    }
}
