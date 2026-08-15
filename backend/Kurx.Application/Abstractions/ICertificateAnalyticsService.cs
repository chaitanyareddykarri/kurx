namespace Kurx.Application.Abstractions;

/// <summary>
/// What happened to an event's certificates (D-344, Phase 11).
///
/// <para><b>Telemetry counts events, not people.</b> A <c>certificate_events</c> row carries a certificate,
/// a type, a time and the request's correlation id — no user, no address, no device, no IP. That is
/// deliberate: an organiser needs to know their certificates are being checked, and does not need to know
/// who is checking a particular person's credential. The correlation id exists so an incident can be
/// traced through logs, and it identifies a request rather than a requester.</para>
///
/// <para><b>Only what the platform can actually observe is recorded.</b> Files are fetched directly from
/// object storage by the browser, so the platform never sees a completed download and does not claim to.
/// <c>Shared</c> has no mechanism behind it at all and is therefore never written — an always-zero counter
/// that looks like a feature is worse than an absent one.</para>
/// </summary>
public interface ICertificateAnalyticsService
{
    /// <summary>Records one observation. Never throws and never blocks the thing being observed:
    /// verification and access must not fail because a counter could not be written.</summary>
    Task RecordAsync(string certificateId, string type, CancellationToken ct = default);

    Task<ServiceResult<CertificateDashboard>> DashboardAsync(
        Guid userId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>The organiser's own record of what was issued, as CSV.</summary>
    Task<ServiceResult<CertificateExport>> ExportAsync(
        Guid userId, Guid eventId, Guid? batchId, bool isAdmin, CancellationToken ct = default);
}

/// <param name="Live">Certificates currently standing. The number that matters — total would count
/// withdrawn and replaced ones as if they were still good.</param>
/// <param name="Verifications">How many times someone checked one of these certificates. The signal an
/// organiser actually wants: it says the credential is being used.</param>
/// <param name="RecentVerifications">Daily counts over the trailing window, oldest first.</param>
public sealed record CertificateDashboard(
    int Templates,
    int Batches,
    int BatchesInProgress,
    int Live,
    int Revoked,
    int Superseded,
    int Sent,
    int PendingDelivery,
    int FailedDelivery,
    int NoDestination,
    int Verifications,
    int Views,
    IReadOnlyList<CertificateDailyCount> RecentVerifications);

public sealed record CertificateDailyCount(DateOnly Day, int Count);

/// <param name="Truncated">True when the export hit its row ceiling. Said out loud rather than silently
/// handing back a short file the organiser would reconcile against nothing.</param>
public sealed record CertificateExport(string FileName, byte[] Content, int Rows, bool Truncated);
