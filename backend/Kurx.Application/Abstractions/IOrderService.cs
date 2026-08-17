namespace Kurx.Application.Abstractions;

/// <summary>Flat key→string answer map for both per_registration and per_participant form fields.
/// GuestName/GuestPhone/GuestEmail are only read when the caller is unauthenticated (D-036).</summary>
public record CreateOrderInput(Guid TicketTypeId, int? GroupSize, string? DisplayName, IReadOnlyDictionary<string, string>? Answers,
    string? GuestName = null, string? GuestPhone = null, string? GuestEmail = null, string? IdempotencyKey = null);

public record TicketView(Guid Id, Guid Code, string State, DateTime? CheckedInAt, string? AnswersJson, DateTime CreatedAt);

/// <summary>Paging bounds for the caller's own order/group lists. These are per-user lists, so the cap
/// exists to close the unbounded-response tail rather than to page a UI — see MyTicketsAsync.</summary>
public static class OrderPaging
{
    public const int MaxPageSize = 200;
}

/// <param name="Currency">ISO-4217, the event's settlement currency (V3 §9.1). Orders have carried a
/// currency since multi-currency landed, but no client was ever told which — every surface hardcoded ₹.
/// Emitted as <c>currency</c> so a non-INR event renders correctly.</param>
/// <param name="EventTitle">Denormalised so an order list is renderable on its own. Without it the
/// "my tickets" surfaces had only <c>EventId</c> to show, i.e. a raw GUID where a title belongs.</param>
public record OrderView(Guid Id, Guid EventId, Guid TicketTypeId, string Status, long AmountPaise, string Currency,
    string? RazorpayOrderId, Guid? GroupId, string? JoinCode, DateTime CreatedAt, IReadOnlyList<TicketView> Tickets,
    string? GuestAccessToken = null, string? EventTitle = null, string? EventSlug = null,
    string? TicketTypeName = null);

public record GroupMemberView(Guid Id, Guid? UserId, string Name, string Phone, Guid? TicketId, string? AnswersJson, DateTime? JoinedAt,
    string? Username = null, string? AvatarKey = null);

public record GroupView(Guid Id, Guid EventId, Guid TicketTypeId, int GroupNumber, string? DisplayName, string JoinCode,
    Guid LeaderUserId, int Capacity, IReadOnlyList<GroupMemberView> Members);

public record JoinGroupInput(string JoinCode, string? DisplayName, IReadOnlyDictionary<string, string>? Answers);

/// <summary>
/// Orders, groups and ticket issuance (docs/DECISIONS.md D-021, D-036, D-049).
/// Free path (D-021): registration issues tickets immediately. Paid path (M10/D-049): a ticket type
/// with PricePaise > 0 is gated live (organizer CanOrganizePaid + org verified, M8), creates a Pending
/// order + gateway order, and issues on the Razorpay capture webhook (ConfirmPaymentAsync).
/// Group capacity depends on the PRICING UNIT (D-372), because that is what the organiser sold:
///   · PerTicket — each person is a separately-priced seat. The leader takes one slot at
///     CreateOrderAsync and each JoinGroupAsync takes one more, so a team of 4 costs 4 units. This is
///     the pre-D-372 behaviour and every existing ticket type is this shape.
///   · PerGroup  — the TEAM is the unit. One slot at CreateOrderAsync and none thereafter, so
///     `Quantity` means "number of teams" and 50 stays 50 as rosters fill.
/// The roster cap is `Order.GroupSize` (falling back to `OrderItem.Qty` for pre-D-372 rows, where the
/// two were necessarily the same number). Joining always requires an authenticated caller (the client
/// logs in via the existing OTP endpoints first) — there is no separate inline OTP step here, since
/// that would duplicate /auth/otp/* (D-009).
/// D-036: CreateOrderAsync's userId is optional — a free, non-competition, Individual-mode ticket type
/// may be purchased anonymously (userId null), producing a guest Order addressable only via its
/// GuestAccessToken (GetGuestOrderAsync/ResendGuestOrderAsync). Competition ticket types route team
/// joining through AcceptGroupInvitationAsync instead of the open JoinGroupAsync code path.
/// </summary>
public interface IOrderService
{
    Task<ServiceResult<OrderView>> CreateOrderAsync(Guid? userId, Guid eventId, CreateOrderInput input, CancellationToken ct = default);

    /// <summary>Confirms a captured gateway payment (from the Razorpay webhook, M10, D-049): verifies the
    /// order is still Pending, records the Payment, consumes the seat hold, issues tickets, and writes the
    /// LedgerEntry(Collected) + OrganizationWallet update in one transaction (D-028). Idempotent — a
    /// re-delivered webhook for an already-Paid order is a no-op.</summary>
    Task<ServiceResult<OrderView>> ConfirmPaymentAsync(string gatewayOrderId, string gatewayPaymentId, CancellationToken ct = default);

    Task<ServiceResult<GroupMemberView>> JoinGroupAsync(Guid userId, JoinGroupInput input, CancellationToken ct = default);

    Task<ServiceResult<GroupMemberView>> AcceptGroupInvitationAsync(Guid userId, string inviteToken, IReadOnlyDictionary<string, string>? answers, CancellationToken ct = default);

    /// <param name="pageSize">Clamped to <see cref="OrderPaging.MaxPageSize"/>. The default is deliberately
    /// generous rather than a typical UI page: both existing clients fetch this list without paging, so a
    /// small default would silently hide a buyer's older tickets. It exists to bound the response, not to
    /// drive a pager — a caller that wants pages passes them explicitly.</param>
    Task<IReadOnlyList<OrderView>> MyTicketsAsync(Guid userId, int page = 0,
        int pageSize = OrderPaging.MaxPageSize, CancellationToken ct = default);

    Task<IReadOnlyList<GroupView>> MyGroupsAsync(Guid userId, int page = 0,
        int pageSize = OrderPaging.MaxPageSize, CancellationToken ct = default);

    Task<ServiceResult<GroupView>> GetGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default);

    Task<ServiceResult<bool>> ResendTicketAsync(Guid userId, Guid ticketCode, CancellationToken ct = default);

    Task<ServiceResult<OrderView>> GetGuestOrderAsync(string accessToken, CancellationToken ct = default);

    Task<ServiceResult<bool>> ResendGuestOrderAsync(string accessToken, CancellationToken ct = default);
}
