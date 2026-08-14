namespace Kurx.Application.Abstractions;

public record PasswordResetResult(bool Ok, string? Error = null, AuthTokens? Tokens = null, Guid? UserId = null);

/// <summary>What <c>/reset/start</c> hands back to the browser. <b>Always populated</b>, including for an
/// identifier that matches no account and for an account with no trusted device — an unknown number must
/// not be distinguishable from a known one by the shape or content of this response, which is the same
/// anti-enumeration rule the endpoint already followed when it returned a bare ack (D-330).
///
/// <para><see cref="ApprovalId"/> and <see cref="MatchNumber"/> are safe to hand an anonymous caller: the
/// id names a challenge that only a <i>trusted device signature</i> can approve, and the match number is
/// worthless without one. The value of showing them is the binding itself — the digits displayed here are
/// the digits the approving device must be told, which is what ties an approval to <i>this</i> reset.</para></summary>
/// <para><see cref="ResetToken"/> is what makes the approval belong to <b>this</b> reset rather than merely
/// to this account. Without it, an owner who legitimately approved their own reset would leave an
/// Approved row that a concurrent attacker-started reset for the same account could spend — account scope
/// is not transaction scope. It is returned once, to the browser that started the ceremony, and only its
/// SHA-256 is stored.</para>
public record PasswordResetStart(Guid ApprovalId, int MatchNumber, DateTime ExpiresAt, string ResetToken);

/// <summary>One pending reset approval, as the trusted device sees it (D-330).</summary>
public record PendingResetView(Guid ApprovalId, string Nonce, int? MatchNumber, string? ContextJson, DateTime ExpiresAt);

/// <summary>Password reset ceremony (Phase 2C, D-127, architecture §5). <b>INV-B: OTP alone never resets a
/// password.</b> Reset requires an OTP to the registered channel <b>and</b> a second factor — a recovery
/// code, or a trusted device that approved <b>this specific reset</b>.
///
/// <para><b>D-330.</b> The second factor used to be "did this account complete any step-up in the last 300
/// seconds", read from <c>IStepUpService.StatusAsync</c>. That is a fact about the account, not a proof
/// held by whoever is asking, and this endpoint is anonymous — so an attacker holding the OTP after a SIM
/// swap inherited the victim's own step-up. It is now a challenge issued <i>by</i> the reset, scoped to the
/// account, single-use, short-lived, and consumed by the completion that presents it.</para>
///
/// On success the account's password is set, every trusted browser and session is cascade-revoked, and a
/// fresh session is issued. A user with neither a trusted device nor recovery codes has no self-service
/// route by design.</summary>
public interface IPasswordResetService
{
    /// <summary>Sends the reset OTP and opens a device-approval challenge. Anti-enumeration: always the
    /// same response shape whether or not the account exists.</summary>
    Task<PasswordResetStart> StartAsync(string identifier, string? requestIp, CancellationToken ct = default);

    /// <summary>Completes the ceremony: verifies the OTP and the second factor, sets the new password, and
    /// issues a fresh session. <paramref name="approvalId"/> and <paramref name="resetToken"/> are the pair
    /// from <see cref="StartAsync"/> and must be presented together; supply them or
    /// <paramref name="recoveryCode"/>, never neither.</summary>
    Task<PasswordResetResult> CompleteAsync(string identifier, string otpCode, string newPassword,
        string? recoveryCode, Guid? approvalId, string? resetToken, CancellationToken ct = default);

    /// <summary>Reset approvals awaiting this user's trusted device. Authenticated — this is the "I am
    /// signed in on my phone and want to reset the password I forgot on my laptop" half of the flow.</summary>
    Task<IReadOnlyList<PendingResetView>> ListPendingAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Approves a pending reset from a trusted device, verifying the device signature and the
    /// match number shown in the browser that started the reset.</summary>
    Task<ServiceResult<bool>> ApproveAsync(Guid userId, Guid approvalId, Guid deviceId, string signatureBase64,
        int? matchNumber, CancellationToken ct = default);
}
