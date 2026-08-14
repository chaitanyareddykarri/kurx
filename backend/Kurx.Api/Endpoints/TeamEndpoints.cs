using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

public record TeamBody(string Name, string? Slug, string? LogoUrl, string? Tagline, Guid? DeclaredOrgUnitId);
public record TeamInviteBody(Guid? PersonId, string? Email, string? Phone, string? Role);
public record TeamJoinBody(string? Message);
public record TeamDecideBody(bool Approve);
public record TeamSubstituteBody(Guid OutMembershipId, Guid InPersonId);
public record TeamTransitionBody(string Action, string? Reason);
public record TeamMergeBody(Guid TeamAId, Guid TeamBId);
public record TeamSplitBody(IReadOnlyList<Guid> MovePersonIds, string NewName);
public record TeamPolicyBody(int? MinSize, int? MaxSize, string? FormationMode, string? JoinApproval, DateTime? LockAt,
    DateTime? NameEditUntil, DateTime? RosterEditUntil, DateTime? MentorEditUntil, int? MaxTeamsPerPersonInEvent,
    bool? UnlimitedTeamsPerTree, bool? AllowSoloAsTeam, bool? AllowCrossOrgMembers, int? SubstitutesAllowed,
    DateTime? SubstitutionDeadline, string? IncompleteTeamPolicy);

/// <summary>V3 §6 (Phase 10) — the Team subsystem surface: formation (create/roster/invite/join-request/substitute),
/// organiser lifecycle + merge/split, and the per-ticket-type TeamPolicy. Additive; the purchase Group flow and the
/// Phase-9 money path are untouched. Authz: user actions act as the caller; captain/organiser gates live in the
/// service (organiser reuses the Phase-6 `event:manage` union).</summary>
public static class TeamEndpoints
{
    public static void MapTeamEndpoints(this WebApplication app)
    {
        var teams = app.MapGroup("/v1").WithTags("teams").RequireAuthorization();

        teams.MapPost("/events/{eventId:guid}/teams", async (Guid eventId, Guid ticketTypeId, TeamBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateTeamAsync(UserId(p), eventId, ticketTypeId, ToInput(body), ct);
            return r.Ok ? Results.Ok(ToTeamJson(r.Value!)) : Fail(r.Error);
        }).Produces<TeamResponse>();

        teams.MapGet("/events/{eventId:guid}/teams", async (Guid eventId, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.GetForEventAsync(eventId, ct);
            return r.Ok ? Results.Ok(r.Value!.Select(ToTeamJson)) : Fail(r.Error);
        }).Produces<IReadOnlyList<TeamResponse>>();

        teams.MapGet("/teams/{teamId:guid}", async (Guid teamId, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.GetTeamAsync(teamId, ct);
            return r.Ok ? Results.Ok(ToTeamJson(r.Value!)) : Fail(r.Error);
        }).Produces<TeamResponse>();

        teams.MapPatch("/teams/{teamId:guid}", async (Guid teamId, TeamBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.UpdateTeamAsync(UserId(p), teamId, IsAdmin(p), ToInput(body), ct);
            return r.Ok ? Results.Ok(ToTeamJson(r.Value!)) : Fail(r.Error);
        }).Produces<TeamResponse>();

        teams.MapPost("/teams/{teamId:guid}/invites", async (Guid teamId, TeamInviteBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.InviteAsync(UserId(p), teamId, new TeamInviteInput(body.PersonId, body.Email, body.Phone, body.Role), ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<TeamInviteView>();

        teams.MapPost("/teams/invites/{token}/accept", async (string token, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.AcceptInviteAsync(UserId(p), token, ct);
            return r.Ok ? Results.Ok(ToTeamJson(r.Value!)) : Fail(r.Error);
        }).Produces<TeamResponse>();

        teams.MapDelete("/teams/invites/{inviteId:guid}", async (Guid inviteId, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.RevokeInviteAsync(UserId(p), inviteId, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        teams.MapPost("/teams/{teamId:guid}/join-requests", async (Guid teamId, TeamJoinBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.RequestJoinAsync(UserId(p), teamId, body.Message, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<TeamJoinRequestView>();

        teams.MapPost("/teams/join-requests/{requestId:guid}/decide", async (Guid requestId, TeamDecideBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.DecideJoinRequestAsync(UserId(p), requestId, body.Approve, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        teams.MapPost("/teams/{teamId:guid}/leave", async (Guid teamId, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.LeaveAsync(UserId(p), teamId, ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        teams.MapDelete("/teams/{teamId:guid}/members/{membershipId:guid}", async (Guid teamId, Guid membershipId, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.RemoveMemberAsync(UserId(p), teamId, membershipId, IsAdmin(p), ct);
            return r.Ok ? Results.NoContent() : Fail(r.Error);
        }).Produces(StatusCodes.Status204NoContent);

        teams.MapPost("/teams/{teamId:guid}/substitute", async (Guid teamId, TeamSubstituteBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.SubstituteAsync(UserId(p), teamId, body.OutMembershipId, body.InPersonId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(ToTeamJson(r.Value!)) : Fail(r.Error);
        }).Produces<TeamResponse>();

        teams.MapPost("/teams/{teamId:guid}/transition", async (Guid teamId, TeamTransitionBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.TransitionAsync(UserId(p), teamId, IsAdmin(p), body.Action, body.Reason, ct);
            return r.Ok ? Results.Ok(ToTeamJson(r.Value!)) : Fail(r.Error);
        }).Produces<TeamResponse>();

        teams.MapPost("/events/{eventId:guid}/teams/merge", async (Guid eventId, TeamMergeBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.MergeAsync(UserId(p), body.TeamAId, body.TeamBId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(ToTeamJson(r.Value!)) : Fail(r.Error);
        }).Produces<TeamResponse>();

        teams.MapPost("/teams/{teamId:guid}/split", async (Guid teamId, TeamSplitBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.SplitAsync(UserId(p), teamId, body.MovePersonIds, body.NewName, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(new { original = ToTeamJson(r.Value!.Original), new_team = ToTeamJson(r.Value!.NewTeam) }) : Fail(r.Error);
        });

        teams.MapGet("/me/teams", async (ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
            Results.Ok((await svc.MyTeamsAsync(UserId(p), ct)).Select(ToTeamJson))).Produces<IReadOnlyList<TeamResponse>>();

        var policy = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/team-policy")
            .WithTags("teams").RequireAuthorization();

        policy.MapGet("/", async (Guid orgId, Guid eventId, Guid ticketTypeId, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var r = await svc.GetPolicyAsync(UserId(p), eventId, ticketTypeId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<TeamPolicyView>();

        policy.MapPatch("/", async (Guid orgId, Guid eventId, Guid ticketTypeId, TeamPolicyBody body, ClaimsPrincipal p, ITeamService svc, CancellationToken ct) =>
        {
            var input = new TeamPolicyInput(body.MinSize, body.MaxSize, body.FormationMode, body.JoinApproval, body.LockAt,
                body.NameEditUntil, body.RosterEditUntil, body.MentorEditUntil, body.MaxTeamsPerPersonInEvent,
                body.UnlimitedTeamsPerTree, body.AllowSoloAsTeam, body.AllowCrossOrgMembers, body.SubstitutesAllowed,
                body.SubstitutionDeadline, body.IncompleteTeamPolicy);
            var r = await svc.SetPolicyAsync(UserId(p), eventId, ticketTypeId, IsAdmin(p), input, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<TeamPolicyView>();
    }

    private static TeamInput ToInput(TeamBody b) => new(b.Name, b.Slug, b.LogoUrl, b.Tagline, b.DeclaredOrgUnitId);

    private static TeamResponse ToTeamJson(TeamView v) => new(
        v.Id, v.EventId, v.TicketTypeId, v.Name, v.Slug, v.LogoUrl, v.Tagline, v.DeclaredOrgUnitId, v.State,
        v.MergedIntoTeamId, v.ActiveMemberCount, v.CreatedAt, v.Members);




    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static bool IsAdmin(ClaimsPrincipal principal) => principal.HasClaim("kurx_admin", "true");

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}
