using System.Security.Claims;
using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Api.Endpoints;

using Kurx.Api;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;

public record CreateOrgBody(string Name, string? Type, string? LegalName, string? PrimaryDomain, string? Bio, string? LinksJson, bool? Personal);
public record UpdateOrgBody(string? Name, string? Bio, string? LinksJson, string? LogoKey);
public record AddMemberBody(string Phone, string Role);
public record ChangeRoleBody(string Role);
public record BankKycBody(string LegalName, string AccountNumber, string Ifsc, string HolderName);
public record PanKycBody(string Pan, string Name);
public record MembershipProfileBody(bool ShowOnProfile);
public record VerificationDocInput(string DocType, string StorageKey);
public record SubmitVerificationBody(IReadOnlyList<VerificationDocInput>? Documents);
public record RepresentationRequestBody(string Name, string? Type, string? LegalName, string? PrimaryDomain,
    string? Bio, string? LinksJson, IReadOnlyList<VerificationDocInput>? Documents);

public static class OrgEndpoints
{
    public static void MapOrgEndpoints(this WebApplication app)
    {
        var orgs = app.MapGroup("/v1/orgs").WithTags("orgs").RequireAuthorization();

        orgs.MapPost("/", async (CreateOrgBody body, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            // D-075: institutions are created only via an admin-approved representation request. The direct
            // create path is now personal-org-only ("Just me" events); an institution create is rejected.
            if (body.Personal != true) return Fail("use_representation_request");
            var type = Enum.TryParse<OrganizationType>(body.Type, ignoreCase: true, out var t) ? t : OrganizationType.Other;
            var result = await svc.CreateAsync(UserId(principal), body.Name, type, body.LegalName, body.PrimaryDomain, body.Bio, body.LinksJson, body.Personal ?? false, ct);
            return result.Ok ? Results.Ok(ToOrgJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<CreateOrgBody>().Produces<OrgDetailResponse>();

        // D-075: event-first "who are you representing?" for a NOT-yet-registered institution. Creates a
        // hidden placeholder org (PendingReview, no Owner) + evidence; an admin approves it into the registry
        // and turns the caller into a Verified Representative. Returns the placeholder so the client can
        // attach the pending event (it stays "Pending Organization Verification" until approval).
        orgs.MapPost("/representation-requests", async (RepresentationRequestBody body, ClaimsPrincipal principal,
            IOrgService svc, CancellationToken ct) =>
        {
            var type = Enum.TryParse<OrganizationType>(body.Type, ignoreCase: true, out var t) ? t : OrganizationType.Other;
            var evidence = (body.Documents ?? [])
                .Select(d => new OrgVerificationEvidence(d.DocType, d.StorageKey)).ToList();
            var result = await svc.SubmitRepresentationRequestAsync(UserId(principal), body.Name, type,
                body.LegalName, body.PrimaryDomain, body.Bio, body.LinksJson, evidence, ct);
            return result.Ok ? Results.Ok(ToOrgJson(result.Value!)) : Fail(result.Error);
        }).Produces<OrgDetailResponse>();

        // Registry search (M4): find an existing org (by name/alias/domain, fuzzy) before creating one.
        orgs.MapGet("/search", async (string? q, int? limit, IOrganizationRegistryService registry, CancellationToken ct) =>
        {
            var results = await registry.SearchAsync(q ?? "", limit ?? 10, ct);
            return Results.Ok(results.Select(r => new OrgSearchHit(
                r.Id, r.Name, r.Slug, r.LogoKey, r.Type.ToLowerInvariant(), r.PrimaryDomain,
                r.VerificationStatus.ToLowerInvariant(), r.Score, r.MatchKind)));
        }).Produces<IReadOnlyList<OrgSearchHit>>();

        // D-268: this endpoint's actual responsibility is "which organizations may I represent?" — it is
        // read by the Representing step of event creation and by the Representing surfaces, never for
        // organization management. It now says so, at /v1/me/representations (mapped below), and the
        // self-representation row is filtered out server-side: representing yourself is not an
        // organization, so it is not a row in this list.
        app.MapGet("/v1/me/representations", async (ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var mine = await svc.ListRepresentableAsync(UserId(principal), ct);
            return Results.Ok(mine.Select(o => new RepresentableOrg(
                o.Id, o.Name, o.Slug, o.LogoKey,
                // The caller's authority to act for this organization — not a role they hold over events.
                o.Authority.ToLowerInvariant())));
        }).RequireAuthorization().WithTags("representations").Produces<IReadOnlyList<RepresentableOrg>>();

        orgs.MapGet("/{orgId:guid}", async (Guid orgId, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(UserId(principal), orgId, ct: ct);
            return result.Ok ? Results.Ok(ToOrgJson(result.Value!)) : Fail(result.Error);
        }).Produces<OrgDetailResponse>();

        // Verification lifecycle (M5): Owner submits evidence; any member views status.
        orgs.MapPost("/{orgId:guid}/verification/submit", async (Guid orgId, SubmitVerificationBody body,
            ClaimsPrincipal principal, IOrgVerificationService svc, CancellationToken ct) =>
        {
            var evidence = (body.Documents ?? [])
                .Select(d => new OrgVerificationEvidence(d.DocType, d.StorageKey)).ToList();
            var r = await svc.SubmitAsync(UserId(principal), orgId, evidence, ct);
            return r.Ok ? Results.Ok(ToVerificationJson(r.Value!)) : Fail(r.Error);
        }).Produces<OrgVerificationResponse>();

        orgs.MapGet("/{orgId:guid}/verification", async (Guid orgId, ClaimsPrincipal principal,
            IOrgVerificationService svc, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(UserId(principal), orgId, ct);
            return r.Ok ? Results.Ok(ToVerificationJson(r.Value!)) : Fail(r.Error);
        }).Produces<OrgVerificationResponse>();

        // The caller's trust capabilities in this org's context (M7): may they represent it?
        orgs.MapGet("/{orgId:guid}/my-capabilities", async (Guid orgId, ClaimsPrincipal principal,
            ITrustService trust, CancellationToken ct) =>
        {
            var caps = await trust.GetOrgCapabilitiesAsync(UserId(principal), orgId, ct);
            return Results.Ok(new OrgCapabilitiesResponse(
                caps.CanRepresentOrg, caps.IsOrgVerifiedRep, caps.IsOrgVerified));
        }).Produces<OrgCapabilitiesResponse>();

        // Workspace capability matrix — the single source of truth for the permission-gated organizer
        // workspace, so Web/Admin/Flutter render zero hardcoded nav or permissions. Structured
        // (organization / trust / permissions / workspaces) for forward-compatible expansion. Derived LIVE
        // from org verification (M5) + membership role (D-015) + user trust (M7), never a token claim.
        // A deferred-backend surface (e.g. payouts) emits an empty action list so its module stays hidden
        // until the API exists — the client trusts this verbatim and never infers a permission itself.
        orgs.MapGet("/{orgId:guid}/workspace-capabilities", async (Guid orgId, ClaimsPrincipal principal,
            ITrustService trust, KurxDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(principal);
            var org = await db.Organizations.AsNoTracking()
                .Where(o => o.Id == orgId && o.DeletedAt == null)
                .Select(o => new { o.Id, o.Name, o.VerificationStatus, o.IsPersonal })
                .FirstOrDefaultAsync(ct);
            if (org is null) return Fail("not_found");

            var orgCaps = await trust.GetOrgCapabilitiesAsync(userId, orgId, ct);
            var userCaps = await trust.GetUserCapabilitiesAsync(userId, ct);
            var membership = await db.Memberships.AsNoTracking()
                .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId, ct);

            var role = membership?.Role;
            var verifiedRep = orgCaps.IsOrgVerifiedRep;
            var isOwner = role == OrgRole.Owner;
            var canManageOrg = role is OrgRole.Owner or OrgRole.Manager;
            var canFinance = role is OrgRole.Owner or OrgRole.Finance;
            var organizerRole = role is OrgRole.Owner or OrgRole.Manager or OrgRole.Representative;

            // Representing yourself satisfies the verified-representative gate vacuously: that gate
            // exists to stop someone acting for an institution they have not proven they belong to,
            // and on a personal representation there is no such claim to prove (D-268, D-289). Without
            // this the matrix answered `tickets: ["view"]` to a host on their own event, so the web
            // client hid the create form and a personally-hosted event could never be sold or
            // registered for — while `IEventAuthority` was accepting the very same write, because it
            // already resolves ownership first and on its own. This aligns what the matrix advertises
            // with what the API enforces; it grants nothing the endpoints were refusing.
            //
            // Deliberately narrowed to `opsEdit` — running your own event — rather than to
            // `verifiedRep` itself, which also gates publish, wallet and the Finance/Community
            // workspaces. Paid events stay gated regardless: `can_host_paid_events` comes from the
            // user's own trust level, never from here.
            var selfRepresenting = org.IsPersonal && isOwner;
            var opsEdit = selfRepresenting || (verifiedRep && organizerRole);

            static string[] Actions(params (string Name, bool On)[] xs)
                => xs.Where(x => x.On).Select(x => x.Name).ToArray();

            var permissions = new Dictionary<string, string[]>
            {
                ["events"]        = Actions(("view", true), ("create", organizerRole), ("update", organizerRole),
                                            ("delete", organizerRole), ("publish", verifiedRep), ("manage", canManageOrg)),
                ["tickets"]       = Actions(("view", true), ("create", opsEdit), ("update", opsEdit), ("delete", opsEdit)),
                ["forms"]         = Actions(("view", true), ("create", opsEdit), ("update", opsEdit), ("delete", opsEdit)),
                ["attendees"]     = Actions(("view", organizerRole), ("manage", opsEdit)),
                ["volunteers"]    = Actions(("view", organizerRole), ("create", canManageOrg), ("update", canManageOrg), ("delete", canManageOrg)),
                ["announcements"] = Actions(("view", true), ("create", opsEdit), ("update", opsEdit), ("delete", opsEdit)),
                ["analytics"]     = Actions(("view", organizerRole)),
                ["wallet"]        = Actions(("view", verifiedRep && canFinance)),
                ["payouts"]       = Actions(),   // deferred: no backend yet — empty keeps the module hidden
                ["reports"]       = Actions(("view", organizerRole), ("export", opsEdit)),
                // Kurx has no org accounts: a user REPRESENTS an org (after admin verification). Users never
                // manage the org record itself (that is admin-only), so this is representation authority —
                // view your representations, request to represent another org — not org management.
                ["representing"]  = Actions(("view", true), ("request", true)),
                ["settings"]      = Actions(("view", true), ("manage", isOwner)),
            };

            // Top-level modules. A module appears only when it has a real destination for this caller.
            var workspaces = new List<object>
            {
                new { key = "dashboard", label = "Dashboard" },
                new { key = "events", label = "Events" },
                new { key = "representing", label = "Representing" },
            };
            if (verifiedRep && canFinance) workspaces.Add(new { key = "finance", label = "Finance" });
            if (verifiedRep) workspaces.Add(new { key = "community", label = "Community" });
            if (verifiedRep && organizerRole) workspaces.Add(new { key = "reports", label = "Reports" });
            workspaces.Add(new { key = "settings", label = "Settings" });

            var representedAs = verifiedRep ? "verified_representative"
                : orgCaps.CanRepresentOrg ? "representative"
                : membership is not null ? "member" : "none";

            return Results.Ok(new
            {
                // D-268: what this event/workspace REPRESENTS. `kind: "personal"` carries no organization
                // identity at all — the client is not told a row exists, because in the domain none does.
                representation = org.IsPersonal
                    ? new
                    {
                        kind = "personal",
                        organization_id = (Guid?)null,
                        name = (string?)null,
                        verified = false,
                        verification_status = (string?)null,
                        represented_as = "self",
                        authority = (string?)null,
                    }
                    : new
                    {
                        kind = "organization",
                        organization_id = (Guid?)org.Id,
                        name = (string?)org.Name,
                        verified = orgCaps.IsOrgVerified,
                        verification_status = (string?)org.VerificationStatus.ToString().ToLowerInvariant(),
                        represented_as = representedAs,
                        authority = role?.ToString().ToLowerInvariant(),
                    },
                trust = new
                {
                    trust_level = userCaps.Level,
                    organizer_level = verifiedRep ? "verified" : "unverified",
                    can_host_paid_events = userCaps.CanOrganizePaid,
                },
                permissions,
                workspaces,
            });
        });

        orgs.MapPatch("/{orgId:guid}", async (Guid orgId, UpdateOrgBody body, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(UserId(principal), orgId, body.Name, body.Bio, body.LinksJson, body.LogoKey, ct);
            return result.Ok ? Results.Ok(ToOrgJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<UpdateOrgBody>().Produces<OrgDetailResponse>();

        orgs.MapGet("/{orgId:guid}/members", async (Guid orgId, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var result = await svc.ListMembersAsync(UserId(principal), orgId, ct);
            return result.Ok ? Results.Ok(result.Value!.Select(ToMemberJson)) : Fail(result.Error);
        }).Produces<IReadOnlyList<OrgMemberResponse>>();

        orgs.MapPost("/{orgId:guid}/members", async (Guid orgId, AddMemberBody body, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var role = Enum.Parse<OrgRole>(body.Role, true);
            var result = await svc.AddMemberAsync(UserId(principal), orgId, body.Phone, role, ct);
            return result.Ok ? Results.Ok(ToMemberJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<AddMemberBody>().Produces<OrgMemberResponse>();

        orgs.MapPatch("/{orgId:guid}/members/{userId:guid}", async (Guid orgId, Guid userId, ChangeRoleBody body, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var role = Enum.Parse<OrgRole>(body.Role, true);
            var result = await svc.ChangeRoleAsync(UserId(principal), orgId, userId, role, ct);
            return result.Ok ? Results.Ok(ToMemberJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<ChangeRoleBody>().Produces<OrgMemberResponse>();

        orgs.MapDelete("/{orgId:guid}/members/{userId:guid}", async (Guid orgId, Guid userId, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var result = await svc.RemoveMemberAsync(UserId(principal), orgId, userId, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        orgs.MapDelete("/{orgId:guid}", async (Guid orgId, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteAsync(UserId(principal), orgId, ct);
            return result.Ok ? Results.Ok(OperationAck.Success) : Fail(result.Error);
        }).Produces<OperationAck>();

        orgs.MapGet("/{orgId:guid}/kyc", async (Guid orgId, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var result = await svc.GetKycAsync(UserId(principal), orgId, ct);
            if (!result.Ok) return Fail(result.Error);
            var view = result.Value!;
            return Results.Ok(new
            {
                payout_account_status = view.PayoutAccountStatus.ToLowerInvariant(),
                bank_last4 = view.BankLast4,
                records = view.Records.Select(r => new
                {
                    id = r.Id,
                    kind = r.Kind,
                    status = r.Status.ToLowerInvariant(),
                    payload_json = r.PayloadJson,
                    created_at = r.CreatedAt,
                    reviewed_at = r.ReviewedAt,
                }),
            });
        });

        orgs.MapPost("/{orgId:guid}/kyc/bank", async (Guid orgId, BankKycBody body, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var result = await svc.SubmitBankKycAsync(UserId(principal), orgId,
                body.LegalName, body.AccountNumber, body.Ifsc, body.HolderName, ct);
            return result.Ok ? Results.Ok(ToKycJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<BankKycBody>().Produces<KycOutcomeResponse>();

        orgs.MapPost("/{orgId:guid}/kyc/pan", async (Guid orgId, PanKycBody body, ClaimsPrincipal principal, IOrgService svc, CancellationToken ct) =>
        {
            var result = await svc.SubmitPanKycAsync(UserId(principal), orgId, body.Pan, body.Name, ct);
            return result.Ok ? Results.Ok(ToKycJson(result.Value!)) : Fail(result.Error);
        }).WithValidation<PanKycBody>().Produces<KycOutcomeResponse>();

        // ── Public org profile (no auth required) ────────────────────────────────
        app.MapGroup("/v1/public/orgs").WithTags("public-orgs")
            .MapGet("/{slug}", async (string slug, IOrgService svc, CancellationToken ct) =>
            {
                var result = await svc.GetPublicAsync(slug, ct);
                if (!result.Ok) return Fail(result.Error);
                var o = result.Value!;
                return Results.Ok(new
                {
                    id = o.Id, name = o.Name, slug = o.Slug,
                    logo_key = o.LogoKey, bio = o.Bio,
                    tier = o.Tier, events_count = o.EventsCount, members_count = o.MembersCount,
                });
            });

        // Toggle whether an org membership appears on the user's public profile
        app.MapPatch("/v1/me/memberships/{membershipId:guid}",
            async (Guid membershipId, MembershipProfileBody body, ClaimsPrincipal principal, KurxDbContext db, CancellationToken ct) =>
            {
                var userId = UserId(principal);
                var membership = await db.Memberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.UserId == userId, ct);
                if (membership is null) return Results.NotFound();
                membership.ShowOnProfile = body.ShowOnProfile;
                await db.SaveChangesAsync(ct);
                return Results.Ok(OperationAck.Success);
            }).WithTags("orgs").RequireAuthorization().Produces<OperationAck>();
    }

    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" or "org_blacklisted" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        "already_member" or "slug_conflict" or "has_active_events" or "organization_domain_taken"
            or "already_pending" or "already_verified" or "duplicate_verified_org"
            => ProblemResults.Problem(error, StatusCodes.Status409Conflict),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };

    private static OrgVerificationResponse ToVerificationJson(OrgVerificationView v) => new(
        v.OrgId, v.Name, v.Slug, v.Status.ToLowerInvariant(), v.ReviewedAt, v.Notes,
        v.Documents.Select(d => new VerificationDocumentResponse(
            d.Id, d.DocType, d.StorageKey, d.Status.ToLowerInvariant(), d.CreatedAt)));

    private static OrgDetailResponse ToOrgJson(OrgDetail o) => new(
        o.Id,
        o.Name,
        o.Slug,
        o.LogoKey,
        o.Bio,
        o.LinksJson,
        o.PayoutAccountStatus.ToLowerInvariant(),
        o.BankLast4,
        o.Tier,
        o.Role.ToLowerInvariant(),
        o.Type.ToLowerInvariant(),
        o.PrimaryDomain,
        o.VerificationStatus.ToLowerInvariant());

    private static OrgMemberResponse ToMemberJson(OrgMember m) => new(
        m.UserId,
        m.Phone,
        m.Name,
        m.Username,
        m.Role.ToLowerInvariant(),
        m.JoinedAt,
        m.AvatarKey,
        m.IsVerified);

    private static KycOutcomeResponse ToKycJson(KycOutcome k) => new(
        k.Status,
        k.Detail,
        k.PayoutAccountStatus.ToLowerInvariant());
}
