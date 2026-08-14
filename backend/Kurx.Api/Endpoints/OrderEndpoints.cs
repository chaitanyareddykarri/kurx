using System.Security.Claims;
using Kurx.Api.ExceptionHandling;
using Kurx.Api.Validation;
using Kurx.Application.Abstractions;

namespace Kurx.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        // Create an order for a ticket type. Not behind .RequireAuthorization(): a free,
        // non-competition, Individual-mode ticket type may be purchased anonymously (D-036).
        // JWT bearer middleware still runs on every request, so an authenticated caller's claim
        // is available via TryUserId() when a valid Bearer token is present.
        app.MapPost("/v1/events/{eventId:guid}/orders",
            async (Guid eventId, CreateOrderInput body, ClaimsPrincipal principal, HttpRequest req, IOrderService svc, CancellationToken ct) =>
            {
                // V3 §17.1 client idempotency: prefer the standard Idempotency-Key header, else the body field.
                var header = req.Headers["Idempotency-Key"].FirstOrDefault();
                var idem = string.IsNullOrWhiteSpace(header) ? body.IdempotencyKey : header;
                var result = await svc.CreateOrderAsync(TryUserId(principal), eventId, body with { IdempotencyKey = idem }, ct);
                return result.Ok ? Results.Ok(ToOrderJson(result.Value!)) : Fail(result.Error);
            })
            .WithValidation<CreateOrderInput>()
            .WithTags("orders")
            .WithSummary("Create an order / register for an event (guest checkout allowed for eligible ticket types)").Produces<OrderResponse>();

        // Guest order access — no auth, possession of the token is the credential (D-036).
        var guest = app.MapGroup("/v1/orders/guest").WithTags("orders");

        guest.MapGet("/{accessToken}",
            async (string accessToken, IOrderService svc, CancellationToken ct) =>
            {
                var result = await svc.GetGuestOrderAsync(accessToken, ct);
                return result.Ok ? Results.Ok(ToOrderJson(result.Value!)) : Fail(result.Error);
            }).WithSummary("View a guest order by its access token").Produces<OrderResponse>();

        guest.MapPost("/{accessToken}/resend",
            async (string accessToken, IOrderService svc, CancellationToken ct) =>
            {
                var result = await svc.ResendGuestOrderAsync(accessToken, ct);
                return result.Ok ? Results.NoContent() : Fail(result.Error);
            }).WithSummary("Re-send a guest order's ticket(s) via WhatsApp and email").Produces(StatusCodes.Status204NoContent);

        var grp = app.MapGroup("/v1")
            .WithTags("orders")
            .RequireAuthorization();

        // Group registration: join an existing group via an open join code (non-competition only).
        grp.MapPost("/groups/join",
            async (JoinGroupInput body, ClaimsPrincipal principal, IOrderService svc, CancellationToken ct) =>
            {
                var result = await svc.JoinGroupAsync(UserId(principal), body, ct);
                return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
            }).WithValidation<JoinGroupInput>().WithSummary("Join a group registration using a join code").Produces<GroupMemberView>();

        // Competition team invitation acceptance — the only way to join an IsCompetition group.
        grp.MapPost("/groups/invitations/{token}/accept",
            async (string token, AcceptGroupInvitationBody? body, ClaimsPrincipal principal, IOrderService svc, CancellationToken ct) =>
            {
                var result = await svc.AcceptGroupInvitationAsync(UserId(principal), token, body?.Answers, ct);
                return result.Ok ? Results.Ok(result.Value!) : Fail(result.Error);
            }).WithSummary("Accept a named competition team invitation").Produces<GroupMemberView>();

        // My tickets across all events. page/pageSize are OPTIONAL and default to one full page of
        // OrderPaging.MaxPageSize — both shipped clients call this with no query string, so a small
        // default would silently hide a buyer's older tickets. The cap closes the unbounded response.
        grp.MapGet("/orders",
            async (ClaimsPrincipal principal, IOrderService svc, int? page, int? pageSize, CancellationToken ct) =>
                Results.Ok((await svc.MyTicketsAsync(UserId(principal), page ?? 0, pageSize ?? OrderPaging.MaxPageSize, ct))
                    .Select(ToOrderJson))
            ).WithSummary("List orders and tickets for the current user (optional page/pageSize, max 200 per page)").Produces<IReadOnlyList<OrderResponse>>();

        // My group memberships.
        grp.MapGet("/groups",
            async (ClaimsPrincipal principal, IOrderService svc, int? page, int? pageSize, CancellationToken ct) =>
                Results.Ok((await svc.MyGroupsAsync(UserId(principal), page ?? 0, pageSize ?? OrderPaging.MaxPageSize, ct))
                    .Select(ToGroupJson))
            ).WithSummary("List group memberships for the current user (optional page/pageSize, max 200 per page)").Produces<IReadOnlyList<GroupResponse>>();

        // Lookup a specific group (member or leader).
        grp.MapGet("/groups/{groupId:guid}",
            async (Guid groupId, ClaimsPrincipal principal, IOrderService svc, CancellationToken ct) =>
            {
                var result = await svc.GetGroupAsync(UserId(principal), groupId, ct);
                return result.Ok ? Results.Ok(ToGroupJson(result.Value!)) : Fail(result.Error);
            }).WithSummary("Get group details").Produces<GroupResponse>();

        // Resend ticket (WhatsApp + email delivery).
        grp.MapPost("/tickets/{ticketCode:guid}/resend",
            async (Guid ticketCode, ClaimsPrincipal principal, IOrderService svc, CancellationToken ct) =>
            {
                var result = await svc.ResendTicketAsync(UserId(principal), ticketCode, ct);
                return result.Ok ? Results.NoContent() : Fail(result.Error);
            }).WithSummary("Re-send a ticket via WhatsApp and email").Produces(StatusCodes.Status204NoContent);
    }

    private static OrderResponse ToOrderJson(OrderView o) => new(
        o.Id,
        o.EventId,
        o.TicketTypeId,
        o.Status.ToLowerInvariant(),
        o.AmountPaise,
        // V3 §9.1: amount_paise is meaningless without it.
        o.Currency,
        o.RazorpayOrderId,
        o.GroupId,
        o.JoinCode,
        o.CreatedAt,
        o.Tickets.Select(ToTicketJson).ToList(),
        o.GuestAccessToken,
        // Denormalised for list rendering; null on the single-order views, which already have the event
        // in hand. Without these a "my tickets" card had only the event GUID to show as a title.
        o.EventTitle,
        o.EventSlug,
        // ticket_type is the ticket type NAME. The rename is the contract.
        o.TicketTypeName);

    private static TicketResponse ToTicketJson(TicketView t) => new(
        t.Id,
        t.Code,
        t.State.ToLowerInvariant(),
        t.CheckedInAt,
        t.AnswersJson,
        t.CreatedAt);

    private static GroupResponse ToGroupJson(GroupView g) => new(
        g.Id,
        g.EventId,
        g.TicketTypeId,
        g.GroupNumber,
        g.DisplayName,
        g.JoinCode,
        g.LeaderUserId,
        g.Capacity,
        g.Members);


    private static Guid UserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id
            : throw new ApiException(StatusCodes.Status401Unauthorized, "invalid_token");

    private static Guid? TryUserId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static IResult Fail(string? error) => error switch
    {
        "forbidden" or "not_eligible" => ProblemResults.Problem(error, StatusCodes.Status403Forbidden),
        "not_found" => ProblemResults.Problem(error, StatusCodes.Status404NotFound),
        _ => ProblemResults.Problem(error, StatusCodes.Status400BadRequest),
    };
}

public record AcceptGroupInvitationBody(IReadOnlyDictionary<string, string>? Answers);
