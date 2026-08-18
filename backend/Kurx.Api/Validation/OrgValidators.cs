using System.Text.Json;
using FluentValidation;
using Kurx.Api.Endpoints;
using Kurx.Domain.Enums;

namespace Kurx.Api.Validation;

// `CreateOrgBodyValidator` went with `POST /v1/orgs` (D-390). The one route that may still name a new
// institution — `POST /v1/orgs/representation-requests` — carries no `WithValidation<>`: the same rules
// (name 2..120, domain format, blacklist, domain dedup) are enforced inside
// `OrgService.SubmitRepresentationRequestAsync`, which is where they were already duplicated. Nothing is
// lost by this deletion; the boundary check that remains is the service's.

public class UpdateOrgBodyValidator : AbstractValidator<UpdateOrgBody>
{
    public UpdateOrgBodyValidator()
    {
        RuleFor(x => x.Name).Length(2, 120).When(x => x.Name is not null);
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.LinksJson)
            .Must(OrgValidationHelpers.IsValidJson)
            .WithMessage("links_json must be valid JSON.")
            .When(x => x.LinksJson is not null);
    }
}

public class AddMemberBodyValidator : AbstractValidator<AddMemberBody>
{
    public AddMemberBodyValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^[0-9+\-\s()]{7,20}$");
        RuleFor(x => x.Role).NotEmpty().IsEnumName(typeof(OrgRole), caseSensitive: false);
    }
}

public class ChangeRoleBodyValidator : AbstractValidator<ChangeRoleBody>
{
    public ChangeRoleBodyValidator()
    {
        RuleFor(x => x.Role).NotEmpty().IsEnumName(typeof(OrgRole), caseSensitive: false);
    }
}

public class BankKycBodyValidator : AbstractValidator<BankKycBody>
{
    public BankKycBodyValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountNumber).NotEmpty().Matches(@"^\d{6,20}$")
            .WithMessage("Account number must be 6-20 digits.");
        RuleFor(x => x.Ifsc).NotEmpty().Matches(@"^[A-Za-z]{4}0[A-Za-z0-9]{6}$")
            .WithMessage("IFSC must be a valid 11-character code.");
        RuleFor(x => x.HolderName).NotEmpty().MaximumLength(200);
    }
}

public class PanKycBodyValidator : AbstractValidator<PanKycBody>
{
    public PanKycBodyValidator()
    {
        RuleFor(x => x.Pan).NotEmpty().Matches(@"^[A-Za-z]{5}\d{4}[A-Za-z]$")
            .WithMessage("PAN must be a valid 10-character code.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

internal static class OrgValidationHelpers
{
    internal static bool IsValidJson(string? json)
    {
        if (json is null) return true;
        try { JsonDocument.Parse(json); return true; }
        catch (JsonException) { return false; }
    }
}
