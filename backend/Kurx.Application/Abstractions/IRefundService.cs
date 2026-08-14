namespace Kurx.Application.Abstractions;

/// <param name="Outcome"><c>refunded</c>, or <c>already_refunded</c> when the call was a replay.</param>
public record RefundResult(Guid OrderId, Guid RefundId, long AmountPaise, string Outcome);

/// <summary>A refund row plus the event/org it belongs to (D-199) — the read side <see cref="RefundOrderAsync"/>
/// never needed until it grew an HTTP surface.</summary>
public record RefundView(Guid Id, Guid OrderId, Guid EventId, Guid OrgId, long AmountPaise, string Currency,
    string Reason, string Status, string? RazorpayRefundId, DateTime CreatedAt);

/// <summary>
/// D-103 (M4): reverses a captured payment. The platform-side truth — an appended reverse ledger entry,
/// the wallet debit, the voided tickets — is recorded immediately; disbursing the money back to the
/// customer is the payment gateway's job and lands with that integration, which is why the recorded
/// <c>Refund</c> row is <c>Initiated</c> rather than <c>Processed</c>.
/// </summary>
public interface IRefundService
{
    /// <summary>Refunds a Paid order in full. Replay-safe: refunding an already-refunded order returns
    /// success with outcome <c>already_refunded</c> rather than reversing twice. Unaware of who's
    /// allowed to call it — that's <see cref="RequestRefundAsync"/>'s job (D-199); this method is also
    /// called directly by the test suite and by any future internal/system-initiated refund path.</summary>
    Task<ServiceResult<RefundResult>> RefundOrderAsync(Guid orderId, string reason, Guid? actorId,
        CancellationToken ct = default);

    /// <summary>D-199: the HTTP-facing entry point. Authorizes the caller — Owner/Finance of the order's
    /// event's org (the same financial-access bar <c>WalletService</c> uses), or a platform FinanceOps/
    /// SuperAdmin (<paramref name="isFinanceStaff"/>, resolved by the endpoint from the live platform-role
    /// claim) — then delegates to the unmodified <see cref="RefundOrderAsync"/>.</summary>
    Task<ServiceResult<RefundResult>> RequestRefundAsync(Guid orderId, string reason, Guid actorId,
        bool isFinanceStaff, CancellationToken ct = default);

    /// <summary>The refund for one order, if any. Visible to the order's own buyer, an Owner/Finance of the
    /// event's org, or FinanceOps/SuperAdmin.</summary>
    Task<ServiceResult<RefundView>> GetForOrderAsync(Guid orderId, Guid requestingUserId, bool isFinanceStaff,
        CancellationToken ct = default);

    /// <summary>The caller's own refunds across every event (mirrors <c>IOrderService.MyTicketsAsync</c>).</summary>
    Task<IReadOnlyList<RefundView>> MyRefundsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Platform-wide refund list. Authorization is the route's job (FinanceOps policy), not this
    /// method's — mirrors every other admin list method in this codebase.</summary>
    Task<(IReadOnlyList<RefundView> Items, int Total)> ListForAdminAsync(string? status, int limit, int page,
        CancellationToken ct = default);
}
