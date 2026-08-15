namespace Kurx.Application.Abstractions;

// Response records live in this namespace on purpose: SnakeCaseResponseConverter (D-259) keys off the
// namespace and nothing else, so a view declared elsewhere silently serialises camelCase.

/// <summary><paramref name="Locked"/> is true for categories the server refuses to switch off —
/// <c>security</c> today (D-263). Clients render the row disabled; the server enforces it regardless,
/// because the caller of a PATCH is exactly who this protects against.</summary>
public record NotificationCategoryPreferenceView(
    string Category, bool InApp, bool Push, bool Email, bool WhatsApp, bool Locked);

public record NotificationPreferencesView(IReadOnlyList<NotificationCategoryPreferenceView> Categories);

public record BlockedUserView(Guid UserId, string Name, string? Username, string? AvatarKey, DateTime CreatedAt,
    /// <summary>Presigned companion to <c>AvatarKey</c> (D-302). Null when there is no key.</summary>
    string? AvatarUrl = null);

public record UsernameHistoryEntryView(string Username, DateTime ReleasedAt);

/// <summary>Deletion state. <paramref name="Pending"/> false means nothing is scheduled — the route
/// answers 404 in that case rather than a "false" body, so a client cannot mistake "no request" for
/// "request cancelled".</summary>
public record AccountDeletionView(bool Pending, DateTime? ScheduledFor, DateTime? RequestedAt);

public record EmailChangeView(string? Email, bool EmailVerified);

/// <summary>One category's requested channel state on a PATCH. Absent channels are left unchanged, so a
/// client can flip one switch without echoing the whole matrix back.</summary>
public record NotificationPreferenceInput(
    string Category, bool? InApp, bool? Push, bool? Email, bool? WhatsApp);

/// <summary>
/// Account settings (D-263): notification preferences, blocks, language, username history, the
/// email-change ceremony, and scheduled deletion.
///
/// <para>Authorization is passed in, never a <c>ClaimsPrincipal</c>. Step-up is enforced at the
/// endpoint via <c>StepUpGuard</c> — it needs the HTTP result type, and putting it here would drag
/// ASP.NET into Application.</para>
/// </summary>
public interface IAccountService
{
    // ── Notification preferences ───────────────────────────────────────────────

    /// <summary>Every category, with the user's stored row or the default where none exists. Absence of
    /// a row means all channels on except WhatsApp — the pre-existing behaviour, which is why no
    /// backfill was needed.</summary>
    Task<NotificationPreferencesView> GetNotificationPreferencesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Upserts the named categories. Refuses to disable a locked category
    /// (<c>category_not_optional</c>) rather than silently ignoring it — a settings screen that appears
    /// to accept a change it discards is worse than one that says no.</summary>
    Task<ServiceResult<NotificationPreferencesView>> UpdateNotificationPreferencesAsync(
        Guid userId, IReadOnlyList<NotificationPreferenceInput> changes, CancellationToken ct = default);

    // ── Blocks ─────────────────────────────────────────────────────────────────

    Task<ServiceResult<bool>> BlockAsync(Guid userId, Guid targetUserId, CancellationToken ct = default);
    Task<bool> UnblockAsync(Guid userId, Guid targetUserId, CancellationToken ct = default);
    Task<IReadOnlyList<BlockedUserView>> ListBlocksAsync(Guid userId, CancellationToken ct = default);

    // ── Username history ───────────────────────────────────────────────────────

    Task<IReadOnlyList<UsernameHistoryEntryView>> GetUsernameHistoryAsync(Guid userId, CancellationToken ct = default);

    // ── Email change ───────────────────────────────────────────────────────────

    /// <summary>Sends an OTP to the NEW address and notifies the OLD one that a change was requested.
    /// The notice goes out on start, not on success: on an already-compromised account it is the only
    /// signal the real owner ever gets, and by the time the change completes it is too late to matter.</summary>
    Task<ServiceResult<bool>> StartEmailChangeAsync(Guid userId, string newEmail, string? requestIp, CancellationToken ct = default);
    Task<ServiceResult<EmailChangeView>> CompleteEmailChangeAsync(Guid userId, string newEmail, string code, CancellationToken ct = default);

    // Phone change is NOT here: POST /v1/me/phone/verify already owns that ceremony
    // (AuthService.VerifyPhoneChangeAsync). D-263 hardened it in place rather than adding a second one.

    // ── Deletion (D-263, India DPDP) ───────────────────────────────────────────

    Task<AccountDeletionView?> GetDeletionAsync(Guid userId, CancellationToken ct = default);
    Task<ServiceResult<AccountDeletionView>> RequestDeletionAsync(Guid userId, string? reason, CancellationToken ct = default);
    Task<bool> CancelDeletionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Anonymises every account whose grace window has elapsed. Clears PII on the user row and
    /// soft-deletes authored posts and comments; orders, ledger entries, certificates and audit logs are
    /// retained by design — their retention is a legal obligation erasure does not override.</summary>
    Task<int> RunScheduledDeletionsAsync(CancellationToken ct = default);
}
