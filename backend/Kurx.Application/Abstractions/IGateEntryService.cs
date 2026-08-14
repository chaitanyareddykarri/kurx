namespace Kurx.Application.Abstractions;

public record CheckInResult(
    bool Admitted,
    bool IsDuplicate,
    string? RejectionReason,
    DateTime? FirstCheckedInAt,
    string? FirstCheckedInByName,
    // V3 §4.4 (Phase 5): set when the holder is no longer audience-eligible at the gate. The admission still
    // succeeds — this flags it for the organiser, it never silently voids (nor silently admits the ineligible).
    string? EligibilityFlag = null);

public interface IGateEntryService
{
    Task<CheckInResult> ScanAsync(Guid scannedByUserId, Guid scanEventId, Guid ticketCode, string? deviceInfo, CancellationToken ct = default);
}
