namespace Kurx.Application.Abstractions;

public record CertificateRosterView(Guid Id, Guid EventId, string VerifyCode, Guid UserId, string HolderName,
    string Kind, string Status, bool IsRevoked, string? RevokedReason, DateTime CreatedAt);

/// <summary>
/// Bulk certificate generation for an event (docs/DECISIONS.md D-035). Organizer-triggered
/// (not an automatic job in this pass); idempotent — re-running skips tickets that already
/// have a Certificate row.
/// </summary>
public interface ICertificateService
{
    Task<ServiceResult<int>> GenerateForEventAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Owner/Manager/Admin: the certificate roster for one event (D-064) — who has a certificate,
    /// its verify code, kind, and revocation state. Closes the "generate but can't list/revoke per event" gap.</summary>
    Task<ServiceResult<IReadOnlyList<CertificateRosterView>>> ListForEventAsync(Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Marks a certificate revoked — visible on the public verify endpoint, never hidden
    /// (the opposite of D-018's draft-hiding pattern: revocation's whole purpose is that a verifier
    /// can confirm a certificate was invalidated, not get a 404 as if it never existed).</summary>
    Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid certificateId, string reason, bool isAdmin, CancellationToken ct = default);
}
