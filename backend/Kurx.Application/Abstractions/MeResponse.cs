namespace Kurx.Application.Abstractions;

/// <summary>The response of <c>GET /v1/me</c> — the caller's own account.
///
/// <para><b>Why this type exists.</b> The endpoint returned an anonymous object, and an anonymous type
/// has a null namespace, so Swashbuckle emitted <c>200 OK</c> with <b>no schema at all</b> and
/// <see cref="SnakeCaseResponseConverterMarker"/>'s namespace rule never applied — the keys were
/// snake_case only because the handler spelled them that way by hand. Both consequences were load-bearing
/// failures: the committed contract could not describe the single most-consumed endpoint on the platform,
/// so the CI drift gate was blind to it, and <c>trust.can_organize_paid</c> could sit nested on the wire
/// while a client declared it flat and silently defaulted it to <c>false</c> for every user. That bug
/// shipped. It is the third of its family (D-245, D-292).</para>
///
/// <para><b>Naming is derived, never spelled.</b> This record lives in
/// <c>Kurx.Application.Abstractions</c>, so the response converter renames every property to snake_case
/// on the way out, at any depth. The emitted keys are therefore byte-identical to the hand-written ones
/// they replace — verified name by name — and no client changes.</para>
///
/// <para><b>No duplicate DTOs.</b> <see cref="Trust"/> reuses <see cref="TrustCapabilities"/>, which
/// already carried exactly these fields (six until D-307 added the two create-event capabilities); a
/// parallel <c>MeTrustView</c> would have been a second
/// definition of one contract, free to drift. <see cref="Identity"/> deliberately does <i>not</i> reuse
/// <c>IdentityStatusView</c>: that record carries fourteen fields including masked evidence, and this
/// endpoint has always exposed only the two below. Reusing it would widen the response, which is an API
/// change wearing the costume of a refactor.</para></summary>
public record MeResponse(
    Guid Id,
    /// <summary>Canonical E.164 where the D-089 backfill has reached this row, legacy digits otherwise.</summary>
    string Phone,
    string Name,
    string? Username,
    string? Email,
    /// <summary>Whether the address was proven by an emailed one-time code, not merely entered. A
    /// recovery channel is only a recovery channel once proven (D-182).</summary>
    bool EmailVerified,
    // ── Self-declared display fields (D-219), served here so the owner's editor prefills from the
    //    caller's own record rather than from a public read that fails for an unclaimed username.
    string? Headline,
    string? Bio,
    string? EducationJson,
    string[]? Skills,
    string[]? Languages,
    string[]? Interests,
    string? LinksJson,
    string? AvatarKey,
    string? CoverKey,
    MePrivacyView Privacy,
    /// <summary>Self-declared date of birth, collected during onboarding. Unlike the display fields above
    /// it has a consumer beyond rendering: age-restricted events carry MinAge/MaxAge eligibility.</summary>
    DateOnly? DateOfBirth,
    /// <summary>When the account was created (UTC) — the "member since" fact. The column has always been
    /// written on insert; it was simply never served, so no client could show it.</summary>
    DateTime CreatedAt,
    bool NeedsOnboarding,
    bool IsPlatformReviewer,
    /// <summary>Live platform roles (M2, D-040) — the admin console derives its whole nav from this.
    /// Authority still comes from the live per-request policy on each admin endpoint; this only decides
    /// what to render.</summary>
    IReadOnlyList<string> PlatformRoles,
    MeIdentityView Identity,
    /// <summary>Derived capabilities (M7), read live. The same values gate the money path server-side,
    /// so a client must never re-derive them.</summary>
    TrustCapabilities Trust,
    /// <summary>Presigned companions to <c>AvatarKey</c>/<c>CoverKey</c> — D-302's rule ("a storage key is
    /// not a URL") applied to the profile, which that sweep fixed for events and missed here. A bare key
    /// is not fetchable: a client rendering <c>avatar_key</c> as a src requests it against its OWN origin
    /// and takes a 404, so every avatar everywhere fell back to initials and looked like a design choice
    /// rather than a defect. The keys stay on the wire because the edit form round-trips them; these are
    /// what a client renders. Null when the corresponding key is unset.
    ///
    /// Trailing and defaulted so every existing construction site binds unchanged.</summary>
    string? AvatarUrl = null,
    string? CoverUrl = null);

/// <summary>Profile visibility as the caller sees it (D-219, four-tier in D-221). Shared by
/// <c>GET /v1/me</c> and <c>PATCH /v1/me/privacy</c>, which returned the same anonymous shape from two
/// places — one contract with two definitions is one contract waiting to diverge.</summary>
public record MePrivacyView(
    bool ProfilePublic,
    bool ShowAttended,
    bool ShowCertificates,
    bool ShowAllies,
    /// <summary>Per-section tier, e.g. <c>{"attended":"connections"}</c>. Null when the resolver has no
    /// stored overrides, which means "fall back to the four booleans above" and never "everything is
    /// hidden". Keys are already snake_case section names and pass through the response converter
    /// unchanged — it renames <i>keys</i>, so a value like <c>event_participants</c> is untouched.</summary>
    IReadOnlyDictionary<string, string>? Sections);

/// <summary>The two-field summary of a person's KYC state that <c>GET /v1/me</c> has always exposed.
/// Deliberately narrower than <c>IdentityStatusView</c> — see <see cref="MeResponse"/>.</summary>
public record MeIdentityView(string Level, string Status);

/// <summary>Documentation anchor only, so the doc comment above can point at the converter by name
/// without <c>Kurx.Application</c> taking a reference on <c>Kurx.Api</c> — the dependency direction
/// runs the other way (CLAUDE.md §2).</summary>
internal static class SnakeCaseResponseConverterMarker;
