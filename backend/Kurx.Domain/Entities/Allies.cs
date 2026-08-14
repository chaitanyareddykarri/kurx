using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>A mutual, explicitly-consented professional relationship between two users (D-201) —
/// "Allies," not a follow. One row per <b>unordered</b> pair for its entire lifetime; the pair is
/// canonicalized as <see cref="UserLowId"/> &lt; <see cref="UserHighId"/> (DB-enforced, also
/// structurally forbids self-allying) so re-requesting after a decline/revoke reactivates the same
/// row instead of accumulating history. <see cref="RequesterId"/>/<see cref="AddresseeId"/> describe
/// who asked whom for the CURRENT <see cref="Status"/> only.</summary>
public class AllyConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }

    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }

    public AllyStatus Status { get; set; } = AllyStatus.Pending;

    /// <summary>Derived mirror of <see cref="HiddenByLow"/>/<see cref="HiddenByHigh"/>: Hidden when
    /// EITHER party has hidden the pair, Public only when neither has. Kept as a stored column so every
    /// existing read and filter (<c>Visibility == Public</c>) works unchanged — it is written by
    /// <c>AllyService.SetVisibilityAsync</c> and must never be assigned directly.</summary>
    public AllyVisibility Visibility { get; set; } = AllyVisibility.Public;

    /// <summary>Per-party hide flags (BUG-A). Visibility is shared but the CHOICE is not: each party
    /// owns exactly one flag and the pair is public only if neither is set, so "the more private choice
    /// wins" — the rule <see cref="Kurx.Application.Abstractions.IAllyService"/> documents. A single
    /// shared column made this last-writer-wins, letting either party silently un-hide a connection the
    /// other had deliberately hidden. Two flags are the smallest shape that is correct when BOTH parties
    /// hide: one un-hiding must not reveal the pair.</summary>
    public bool HiddenByLow { get; set; }
    public bool HiddenByHigh { get; set; }

    /// <summary>How the connection was formed: "request" (one side asked, the other accepted) or
    /// "mutual_request" (both sides asked independently before either accepted). Set on Accept.</summary>
    public string? ConnectedVia { get; set; }

    /// <summary>Best-effort earliest public event both users have real involvement in, computed once
    /// at Accept time. Null when none is found — never fabricated.</summary>
    public Guid? FirstSharedEventId { get; set; }

    /// <summary>Reserved for future cross-subsystem wiring (chat, event coincidence, …) — no existing
    /// single source of truth defines "interaction" yet, so this feature does not write it.</summary>
    public DateTimeOffset? LastInteractionAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
