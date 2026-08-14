namespace Kurx.Application.Abstractions;

public record InvitationView(
    Guid Id, Guid EventId, string Name, string? Email, string? Phone,
    string Channel, string InviteToken, string SendStatus, string RsvpStatus,
    string Status, int SendCount, DateTime? SentAt, DateTime? RespondedAt, DateTime CreatedAt,
    Guid? GroupId = null);

public record InvitationImportResult(int Created, int DuplicatesSkipped, List<ImportRowError> InvalidRows);
public record ImportRowError(int Line, string Reason);

public record InvitationFunnel(int Invited, int Sent, int Accepted, int Declined, int Registered);

public record PublicInvitationView(
    string GuestName, string RsvpStatus,
    string EventTitle, string EventSlug, string? BannerUrl,
    DateTime StartsAt, string? VenueName, string? City, string OrgName);

// ── D-266 M6 (D9) ────────────────────────────────────────────────────────────────────────────
// `Invite Only` is ONE registration policy with two delivery methods. A policy answers "who may
// register"; a method answers "how were they told". They are never separate policies.

/// <summary>One invitation in the invitee's own inbox (Method A). Carries the event, because an inbox that
/// only names invitation ids makes the recipient fetch each event to know what they were invited to.</summary>
public record MyInvitationView(
    Guid Id, Guid EventId, string EventTitle, string EventSlug, string? BannerKey,
    DateTime StartsAt, string? VenueName, string? City,
    string InvitedByName, string RsvpStatus, DateTime CreatedAt);

/// <summary>An invite link as its creator sees it. <c>Token</c> is present because the organiser has to be
/// able to share it; <c>PasscodeHash</c> never is.</summary>
public record InviteLinkView(
    Guid Id, Guid EventId, string Token, int? MaxSeats, int UsedCount, bool SingleUse,
    DateTime? ExpiresAt, bool RequiresPasscode, string Status, DateTime CreatedAt);

/// <summary>The pre-flight a holder sees BEFORE authenticating: is this link usable, and will it ask for a
/// passcode? Deliberately thin — it is unauthenticated, so it names the event and nothing about who else
/// has redeemed.</summary>
public record PublicInviteLinkView(
    string EventTitle, string EventSlug, DateTime StartsAt, string? VenueName, string? City,
    bool RequiresPasscode, bool IsUsable, string? Reason, int? SeatsRemaining);

public record CreateInviteLinkInput(int? MaxSeats, bool SingleUse, DateTime? ExpiresAt, string? Passcode);

public interface IInvitationService
{
    /// <summary>When groupId is set, this is a competition team invite (D-036): authorization is the
    /// group's captain (LeaderUserId) or an org manager, and username may be supplied instead of
    /// phone/email to look up an existing Kurx user (auto-filling name/phone from that account).</summary>
    Task<ServiceResult<InvitationView>> AddAsync(Guid invitedBy, Guid eventId, string? name, string? email, string? phone, string channel,
        string? username = null, Guid? groupId = null, CancellationToken ct = default);
    Task<ServiceResult<InvitationImportResult>> ImportCsvAsync(Guid invitedBy, Guid eventId, Stream csv, CancellationToken ct = default);
    Task<ServiceResult<int>> EnqueueSendAsync(Guid userId, Guid eventId, Guid[]? invitationIds, CancellationToken ct = default);
    Task<ServiceResult<InvitationView>> ResendAsync(Guid userId, Guid invitationId, CancellationToken ct = default);
    Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid invitationId, CancellationToken ct = default);
    Task<ServiceResult<(List<InvitationView> Items, int Total, InvitationFunnel Funnel)>> ListAsync(Guid userId, Guid eventId, string? status, string? rsvp, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<ServiceResult<PublicInvitationView>> GetPublicAsync(string token, CancellationToken ct = default);
    Task<ServiceResult<string>> RsvpAsync(string token, string response, CancellationToken ct = default);

    // Internal — called by payment/order completion hooks
    Task LinkOrderAsync(string inviteToken, Guid orderId, CancellationToken ct = default);

    // ── D-266 M6 · Method A: invite a Kurx user by username ──────────────────────────────────
    // `AddAsync` already accepts a username for the D-036 team path; these are the in-app accept/decline
    // half that Method A needs and the per-invitee token flow never had.

    /// <summary>The caller's own pending invitations.</summary>
    Task<IReadOnlyList<MyInvitationView>> ListMineAsync(Guid userId, bool pendingOnly, int page, int pageSize,
        CancellationToken ct = default);

    /// <summary>Accept or decline an invitation addressed to the caller.
    /// <para><b>Accepting grants permission to register — never a ticket and never a discount.</b> The order
    /// still runs and payment still applies (D9). Returns <c>not_invited</c> when the invitation is not the
    /// caller's, <c>invitation_already_responded</c> when it is settled.</para></summary>
    Task<ServiceResult<MyInvitationView>> RespondAsync(Guid userId, Guid invitationId, bool accept,
        CancellationToken ct = default);

    // ── D-266 M6 · Method B: the shareable invite link ───────────────────────────────────────

    Task<ServiceResult<InviteLinkView>> CreateLinkAsync(Guid userId, Guid eventId, bool isAdmin,
        CreateInviteLinkInput input, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<InviteLinkView>>> ListLinksAsync(Guid userId, Guid eventId, bool isAdmin,
        CancellationToken ct = default);

    Task<ServiceResult<bool>> RevokeLinkAsync(Guid userId, Guid linkId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Unauthenticated pre-flight: is this link usable, and does it need a passcode?</summary>
    Task<ServiceResult<PublicInviteLinkView>> GetPublicLinkAsync(string token, CancellationToken ct = default);

    /// <summary>Claim a seat. <b>Idempotent per user</b> — redeeming twice consumes no second seat (D9
    /// rule 7) — and the seat is claimed in SQL, never read-modify-write (D-240). Grants permission to
    /// register and nothing else: payment is untouched.</summary>
    Task<ServiceResult<InviteLinkView>> RedeemLinkAsync(Guid userId, string token, string? passcode,
        CancellationToken ct = default);

    /// <summary>Has this user been let in to this event by either method? The read the eligibility engine
    /// makes; it exists here so <c>AudienceService</c> does not query invitation tables directly and end up
    /// with a second definition of "invited".</summary>
    Task<bool> HasAccessAsync(Guid userId, Guid eventId, CancellationToken ct = default);
}
