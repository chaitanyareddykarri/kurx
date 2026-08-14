using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

public record WalkInBody(Guid TicketTypeId, string? GuestName, string? GuestPhone, string? GuestEmail, bool Free, string? IdempotencyKey);
public record SeatBlockBody(Guid TicketTypeId, Guid RegistrantOrgUnitId, Guid? PayerId, Guid DelegateUserId,
    string? PaymentMode, int Quantity, DateTime? AssignmentDeadline, int? ReassignLimit);
public record SeatAssignBody(Guid PersonId, string? AnswersJson);

/// <summary>V3 §7.5/§7.6 (Phase 13) — delegated & walk-in registration. Walk-in: a staff participant registers an
/// attendee at the gate (offline-safe via an idempotency key). SeatBlock: an organiser reserves N unassigned seats for
/// an org unit; the delegate console assigns/reassigns/tracks status. All minting reuses the authoritative Order/Ticket
/// projection; authorization (staff / organiser / delegate) is enforced in the services. Additive; no existing route
/// or response shape changed.</summary>
public static class DelegatedRegistrationEndpoints
{
    public static void MapDelegatedRegistrationEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/v1").WithTags("delegated-registration").RequireAuthorization();

        // ── Walk-in (§7.6) ────────────────────────────────────────────────────────
        g.MapPost("/events/{eventId:guid}/walk-ins", async (Guid eventId, WalkInBody b, ClaimsPrincipal p, IWalkInService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(UserId(p), eventId, IsAdmin(p), new WalkInInput(b.TicketTypeId, b.GuestName, b.GuestPhone, b.GuestEmail, b.Free, b.IdempotencyKey), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<WalkInView>();

        // ── SeatBlock / delegated (§7.5) ──────────────────────────────────────────
        g.MapPost("/events/{eventId:guid}/seat-blocks", async (Guid eventId, SeatBlockBody b, ClaimsPrincipal p, ISeatBlockService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(UserId(p), eventId, IsAdmin(p),
                new SeatBlockInput(b.TicketTypeId, b.RegistrantOrgUnitId, b.PayerId, b.DelegateUserId, b.PaymentMode, b.Quantity, b.AssignmentDeadline, b.ReassignLimit), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<SeatBlockView>();

        g.MapGet("/events/{eventId:guid}/seat-blocks", async (Guid eventId, ClaimsPrincipal p, ISeatBlockService svc, CancellationToken ct) =>
        {
            var r = await svc.ListForEventAsync(UserId(p), eventId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<SeatBlockView>>();

        g.MapGet("/seat-blocks/{seatBlockId:guid}", async (Guid seatBlockId, ClaimsPrincipal p, ISeatBlockService svc, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(UserId(p), seatBlockId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<SeatBlockView>();

        g.MapGet("/seat-blocks/{seatBlockId:guid}/seats", async (Guid seatBlockId, ClaimsPrincipal p, ISeatBlockService svc, CancellationToken ct) =>
        {
            var r = await svc.ListSeatsAsync(UserId(p), seatBlockId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<IReadOnlyList<SeatView>>();

        g.MapGet("/seat-blocks/{seatBlockId:guid}/status", async (Guid seatBlockId, ClaimsPrincipal p, ISeatBlockService svc, CancellationToken ct) =>
        {
            var r = await svc.StatusAsync(UserId(p), seatBlockId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<SeatBlockStatusView>();

        g.MapPost("/seat-blocks/seats/{seatId:guid}/assign", async (Guid seatId, SeatAssignBody b, ClaimsPrincipal p, ISeatBlockService svc, CancellationToken ct) =>
        {
            var r = await svc.AssignAsync(UserId(p), seatId, IsAdmin(p), new SeatAssignInput(b.PersonId, b.AnswersJson), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<SeatView>();

        g.MapPost("/seat-blocks/seats/{seatId:guid}/unassign", async (Guid seatId, ClaimsPrincipal p, ISeatBlockService svc, CancellationToken ct) =>
        {
            var r = await svc.UnassignAsync(UserId(p), seatId, IsAdmin(p), ct);
            return r.Ok ? Results.Ok(r.Value) : Fail(r.Error);
        }).Produces<SeatView>();
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
