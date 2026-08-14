using Kurx.Domain.Enums;

namespace Kurx.Application.Abstractions;

/// <summary>The resolved answer to "which sections of this profile may this viewer see", computed once
/// per request (D-221).
///
/// <para>This type is the profile's <b>only</b> authorization boundary. It cannot be constructed
/// outside <c>ProfileVisibilityResolver</c> — the constructor is private and the resolver is the sole
/// factory — so a read path cannot produce one by accident, and every gated method takes one as a
/// required parameter. "Forgot to check" therefore fails to compile rather than leaking.</para>
///
/// <para>It is a snapshot: the relationship checks it depends on (an accepted connection, a shared
/// event) run at most once each, when first needed, not per section.</para></summary>
public sealed class SectionAccess
{
    private readonly IReadOnlyDictionary<ProfileSection, bool> _visible;

    private SectionAccess(IReadOnlyDictionary<ProfileSection, bool> visible, bool isOwner)
    {
        _visible = visible;
        IsOwner = isOwner;
    }

    /// <summary>True when the viewer is the profile owner — they always see everything, including
    /// sections set to OnlyMe.</summary>
    public bool IsOwner { get; }

    /// <summary>Fails closed: a section absent from the map is not visible. The map is built from the
    /// full <see cref="ProfileSection"/> enum, so absence means someone added a member without a
    /// default — which the exhaustiveness test catches at build time.</summary>
    public bool CanSee(ProfileSection section) => _visible.TryGetValue(section, out var ok) && ok;

    /// <summary>Sole factory. Internal to the Application layer's contract and called only by
    /// <c>ProfileVisibilityResolver</c>; nothing else in the solution can mint one.</summary>
    public static SectionAccess Create(IReadOnlyDictionary<ProfileSection, bool> visible, bool isOwner)
        => new(visible, isOwner);
}

public interface IProfileVisibilityResolver
{
    /// <summary>Resolves every section for one (owner, viewer) pair. <paramref name="viewerId"/> is null
    /// for an anonymous reader.</summary>
    Task<SectionAccess> ResolveAsync(Guid ownerId, Guid? viewerId, CancellationToken ct = default);

    /// <summary>The batch primitive: of <paramref name="userIds"/>, which have a <b>Profile</b> section
    /// visible to <paramref name="viewerId"/> (D-233).
    ///
    /// <para>This answers the one question every person-list surface in the product asks — "may this
    /// other person's name, username and avatar be shown here" — and it is the replacement for the 15
    /// direct <c>ProfilePublic</c> reads that used to answer it independently across eight services.
    /// Those reads are why the legacy boolean columns could not be dropped.</para>
    ///
    /// <para><b>Cost is independent of the number of ids.</b> One query loads the users; the two
    /// relationship checks (accepted connection, shared public event) run at most once each for the
    /// whole batch, and only if some user in it actually uses the tier that needs them. A batch in
    /// which everyone is Public — overwhelmingly the common case — costs exactly one query. Never call
    /// <see cref="ResolveAsync"/> in a loop to answer this; that is the N+1 this exists to prevent.</para>
    ///
    /// <para>An id that does not exist is simply absent from the result, so callers treat "deleted" and
    /// "hidden" identically without a second lookup (D-018).</para></summary>
    Task<IReadOnlySet<Guid>> VisibleProfileIdsAsync(
        IReadOnlyCollection<Guid> userIds, Guid? viewerId, CancellationToken ct = default);

    /// <summary>The effective per-section settings for one user, with the boolean fallback already
    /// applied — what the owner's own privacy screen renders and edits.</summary>
    Task<IReadOnlyDictionary<ProfileSection, SectionVisibility>> GetSettingsAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>Partial update of the owner's own section settings. Dual-writes the four legacy
    /// booleans for the sections they cover, so a rollback to the pre-D-221 read path sees the same
    /// answer. Returns the effective settings after the write.</summary>
    Task<IReadOnlyDictionary<ProfileSection, SectionVisibility>> UpdateSettingsAsync(
        Guid userId, IReadOnlyDictionary<ProfileSection, SectionVisibility> changes, CancellationToken ct = default);
}
