namespace Kurx.Application.Abstractions;

/// <summary>
/// How the person named on a certificate reaches it (D-355, Phase 10).
///
/// <para>Two routes, because participants are not all account holders. Someone with a Kurx account gets
/// their certificates in their account. Someone who was on a spreadsheet and nothing more gets a
/// <b>capability link</b> — a long-lived, revocable URL that is the credential. That is the whole point of
/// the confirmed decision to support external participants: requiring an account to collect a certificate
/// would exclude most of the people certificates are issued to.</para>
///
/// <para><b>Linking is by verified email only.</b> A recipient row is claimed by an account when that
/// account has proved it controls the address the organiser wrote down. An unverified address would let
/// anyone type a stranger's email and collect their certificate — the link would be an assertion rather
/// than a proof.</para>
/// </summary>
public interface ICertificateParticipantService
{
    /// <summary>Mints a capability link for a recipient. The raw token is returned <b>once</b>; only its
    /// hash is stored, so the platform genuinely cannot show it again afterwards.</summary>
    Task<ServiceResult<CertificateAccessLinkView>> CreateAccessLinkAsync(
        Guid userId, Guid recipientId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Revokes a link. The certificates are untouched — this closes a door, it does not withdraw
    /// anything.</summary>
    Task<ServiceResult<bool>> RevokeAccessLinkAsync(
        Guid userId, Guid linkId, bool isAdmin, CancellationToken ct = default);

    /// <summary>A recipient's links, without their tokens — which cannot be shown, only replaced.</summary>
    Task<ServiceResult<IReadOnlyList<CertificateAccessLinkView>>> ListAccessLinksAsync(
        Guid userId, Guid recipientId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Resolves a capability link. Anonymous by design: holding the token IS the authorisation.</summary>
    Task<ServiceResult<ParticipantCertificatesView>> ResolveAccessAsync(
        string token, CancellationToken ct = default);

    /// <summary>The signed-in user's own certificates, linking any that are waiting on their verified
    /// address first.</summary>
    Task<ServiceResult<ParticipantCertificatesView>> ListMineAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>Claims recipient rows matching a user's verified email. Idempotent, and safe to call on
    /// every sign-in: rows already linked to somebody are never re-pointed.</summary>
    Task<int> LinkByVerifiedEmailAsync(Guid userId, CancellationToken ct = default);
}

/// <param name="Url">The full link, present ONLY on the response that created it.</param>
/// <param name="LastAccessedAt">Whether it has ever been used, and nothing more — no address, no device.</param>
public sealed record CertificateAccessLinkView(
    Guid Id,
    Guid RecipientId,
    string? Url,
    bool Revoked,
    DateTime CreatedAt,
    DateTime? LastAccessedAt,
    DateTime? RevokedAt);

/// <param name="RecipientName">As printed. The organiser's spelling is authoritative.</param>
public sealed record ParticipantCertificatesView(
    string RecipientName,
    IReadOnlyList<ParticipantCertificateView> Certificates);

/// <param name="DownloadPdfUrl">Presigned and short-lived, and <b>null for a certificate that is no longer
/// live</b>. A withdrawn or replaced certificate is still shown — hiding it would leave the holder unable
/// to find out what happened — but handing back a fresh download of a document the platform publicly
/// calls invalid would be arming a misunderstanding.</param>
/// <param name="ReplacedBy">Where the live version is, when this one was corrected.</param>
public sealed record ParticipantCertificateView(
    string CertificateId,
    string EventTitle,
    string Status,
    DateTime IssuedAt,
    string VerificationUrl,
    string? DownloadPdfUrl,
    string? DownloadPngUrl,
    string? ReplacedBy,
    string? RevocationReason);
