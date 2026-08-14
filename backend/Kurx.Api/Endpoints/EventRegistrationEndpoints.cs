using System.Security.Claims;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

using Kurx.Api.ExceptionHandling;

public record RegistrationPolicyBody(string? Subject, IReadOnlyList<string>? Gates, string? IdentityRequirement,
    string? Allocation, string? Payment, DateTime? OpensAt, DateTime? ClosesAt, int? LateWindowMinutes,
    DateTime? EditUntil, DateTime? CancelUntil, IReadOnlyList<string>? DocumentsRequired);

/// <summary>V3 §7 (Phase 8) — the event-registration layer's organiser surface: read/set the five-axis policy
/// per ticket type and list an event's registrations (the shadow of Order/Ticket). (Distinct from the
/// auth-ceremony <c>RegistrationEndpoints</c>.) Authz reuses the Phase-6 `event:manage` union. The
/// registration → admission → credential rows are projected by the order flows.</summary>
public static class EventRegistrationEndpoints
{
    public static void MapEventRegistrationEndpoints(this WebApplication app)
    {
        var policy = app.MapGroup("/v1/orgs/{orgId:guid}/events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/registration-policy")
            .WithTags("registration").RequireAuthorization();

        policy.MapGet("/", async (Guid orgId, Guid eventId, Guid ticketTypeId, ClaimsPrincipal p, IEventRegistrationService svc, CancellationToken ct) =>
        {
            var r = await svc.GetPolicyAsync(UserId(p), eventId, ticketTypeId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<RegistrationPolicyView>();

        policy.MapPatch("/", async (Guid orgId, Guid eventId, Guid ticketTypeId, RegistrationPolicyBody body, ClaimsPrincipal p, IEventRegistrationService svc, CancellationToken ct) =>
        {
            var input = new RegistrationPolicyInput(body.Subject, body.Gates, body.IdentityRequirement, body.Allocation,
                body.Payment, body.OpensAt, body.ClosesAt, body.LateWindowMinutes, body.EditUntil, body.CancelUntil, body.DocumentsRequired);
            var r = await svc.SetPolicyAsync(UserId(p), eventId, ticketTypeId, IsAdmin(p), input, ct);
            return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
        }).Produces<RegistrationPolicyView>();

        app.MapGet("/v1/orgs/{orgId:guid}/events/{eventId:guid}/registrations",
            async (Guid orgId, Guid eventId, ClaimsPrincipal p, IEventRegistrationService svc, CancellationToken ct) =>
            {
                var r = await svc.GetForEventAsync(UserId(p), eventId, IsAdmin(p), ct);
                return r.Ok ? Results.Ok(r.Value!) : Fail(r.Error);
            }).WithTags("registration").RequireAuthorization().Produces<IReadOnlyList<RegistrationView>>();
    }



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
