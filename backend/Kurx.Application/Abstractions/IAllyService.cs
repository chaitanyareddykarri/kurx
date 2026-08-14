namespace Kurx.Application.Abstractions;

/// <summary>One ally connection from the caller's perspective — <c>OtherUserId</c> is always the
/// counterpart, never the caller.</summary>
public record AllyConnectionView(
    Guid Id, Guid OtherUserId, string OtherName, string? OtherUsername, string? OtherAvatarKey,
    string Status, string Visibility, DateTimeOffset RequestedAt, DateTimeOffset? RespondedAt,
    Guid? FirstSharedEventId, string? FirstSharedEventTitle, string? FirstSharedEventSlug);

public record AllyProfileCard(Guid UserId, string Name, string? Username, string? AvatarKey, int MutualEventCount);

public record SharedEventSummary(Guid Id, string Title, string Slug, DateTime StartsAt);
public record SharedOrgSummary(Guid Id, string Name, string Slug);

/// <summary>One derived professional relationship (D-226) — <i>why</i> two people know each other.
/// Directional: produced from one subject's perspective, so the inverse is a different
/// <see cref="Type"/>. <see cref="Count"/> is how many shared contexts produced it and
/// <see cref="Context"/> names one of them as evidence. There is deliberately no strength score.</summary>
public record ProfileRelationship(string Type, string Label, int Count, string? Context);

public record MutualDetail(
    IReadOnlyList<SharedEventSummary> SharedEvents,
    IReadOnlyList<SharedOrgSummary> SharedOrgs,
    IReadOnlyList<ProfileRelationship> Relationships);

/// <summary>A ranked candidate for a new ally connection (D-20x), v1 signals only — shared event
/// co-participation and shared verified-organization membership. <see cref="Reason"/> is a
/// human-readable explanation ("3 shared events", "Same organization"), never a bare score.</summary>
public record AllySuggestion(Guid UserId, string Name, string? Username, string? AvatarKey,
    int SharedEventCount, int SharedOrgCount, string Reason);

/// <summary>Mutual, explicitly-consented professional connections ("Allies," D-201) — not a follow.
/// One <see cref="Kurx.Domain.Entities.AllyConnection"/> row per unordered user pair, ever; see the
/// entity doc and <c>docs/architecture/PROFESSIONAL_IDENTITY_SPEC.md</c> §5 for the state machine.</summary>
public interface IAllyService
{
    Task<ServiceResult<AllyConnectionView>> RequestAsync(Guid requesterId, Guid targetUserId, CancellationToken ct = default);
    Task<ServiceResult<AllyConnectionView>> AcceptAsync(Guid userId, Guid connectionId, CancellationToken ct = default);
    Task<ServiceResult<AllyConnectionView>> DeclineAsync(Guid userId, Guid connectionId, CancellationToken ct = default);
    Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid connectionId, CancellationToken ct = default);

    /// <summary>Sets one connection's public visibility (D-219) — hide a single ally without turning off
    /// <c>ShowAllies</c> entirely, which is what the column was added for but nothing could set.
    /// <paramref name="visibility"/> is the case-insensitive enum name ("public"/"hidden"). Either party
    /// may set it and the flag is shared: the more private choice wins, matching the existing rule that
    /// either party's <c>ProfilePublic=false</c> hides the pair. Non-party caller → <c>not_found</c>.</summary>
    Task<ServiceResult<AllyConnectionView>> SetVisibilityAsync(
        Guid userId, Guid connectionId, string visibility, CancellationToken ct = default);

    // Paged. These previously returned every row, so a user with thousands of allies got an unbounded
    // response. The defaults keep every existing caller working unchanged — page 1 of 50 — and callers
    // that want more pass page/pageSize. pageSize is clamped by the implementation.
    Task<IReadOnlyList<AllyConnectionView>> ListIncomingAsync(Guid userId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<IReadOnlyList<AllyConnectionView>> ListOutgoingAsync(Guid userId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<IReadOnlyList<AllyConnectionView>> ListMineAsync(Guid userId, int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>The owner's allies as seen by <paramref name="viewerId"/>.</summary>
    ///
    /// <para><paramref name="viewerId"/> is not optional in spirit: without it the Connections and
    /// EventParticipants tiers collapse to "hidden from everyone", which is the defect this parameter
    /// was added to fix — the owner could not even see their own list. Null means anonymous.</para>
    ///
    /// <returns>Fail("not_found") when the Profile section is not visible to this viewer (a hidden
    /// profile is indistinguishable from a missing one, D-018); Success(empty) when Profile is visible
    /// but Network is not — a hidden section, not an error.</returns>
    Task<ServiceResult<IReadOnlyList<AllyProfileCard>>> GetAlliesForProfileAsync(
        string username, int page, int pageSize, Guid? viewerId, CancellationToken ct = default);

    /// <summary>One query for a whole list of candidate users — the batch primitive every person-list
    /// surface (attendees/team/org-members/speakers/search/suggestions) uses instead of N calls.
    /// Values: "none" | "pending_outgoing" | "pending_incoming" | "accepted".</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetStatusBatchAsync(
        Guid callerId, IReadOnlyList<Guid> targetUserIds, CancellationToken ct = default);

    /// <summary>Shared events + shared verified organizations between the caller and another user.
    /// <b>Requires an Accepted connection between the two</b> (D-211) — without one it returns an empty
    /// result rather than an error, so it can't be used as an existence oracle. Same visibility rules as
    /// everywhere else (private events/certs never surface, only <c>ShowOnProfile</c> memberships count).</summary>
    Task<MutualDetail> GetMutualDetailAsync(Guid callerId, Guid otherUserId, CancellationToken ct = default);

    /// <summary>Ranked suggestions from real shared history only — shared event co-participation and
    /// shared organization membership (v1: the two signals with real data behind them today).
    /// Excludes self and anyone already connected/pending in either direction.</summary>
    Task<IReadOnlyList<AllySuggestion>> GetSuggestionsAsync(Guid userId, int limit, CancellationToken ct = default);
}
