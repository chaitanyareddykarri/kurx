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

/// <summary>The result of scanning a staff badge's signed pass (D-385).
///
/// <para>Separate from <see cref="CheckInResult"/> because it answers a different question. An attendee
/// scan asks "may this ticket come in"; a staff scan asks "who is this and what does the badge authorise",
/// and the marshal at the door needs the answer on screen — a name and an access level they can read
/// against the person standing there.</para></summary>
/// <param name="Reason">Why a refused scan was refused. <c>invalid_pass</c> covers both a malformed
/// payload and a bad signature, deliberately: telling a forger which half they got wrong is free help.</param>
public record StaffCheckInResult(
    bool Admitted,
    bool IsDuplicate,
    string? Reason,
    string? Name = null,
    string? Role = null,
    string? AccessLevel = null,
    DateTime? FirstScannedAt = null,
    string? FirstScannedByName = null);

public interface IGateEntryService
{
    Task<CheckInResult> ScanAsync(Guid scannedByUserId, Guid scanEventId, Guid ticketCode, string? deviceInfo, CancellationToken ct = default);

    /// <summary>Admits a staff member from the signed pass printed on their badge (D-385) — the half
    /// D-362 shipped without.
    ///
    /// <para>Every check is live and none of them is the signature alone: a valid signature proves only
    /// that Kurx minted the pass. Whether the assignment is still <c>Accepted</c>, and whether it belongs
    /// to <b>this</b> event, are asked of the database at scan time, so removing someone from the crew
    /// stops their printed badge working immediately (D-015) and a badge for Saturday's event does not
    /// open Sunday's door.</para></summary>
    /// <param name="pass">The raw scanned payload, <c>staff:{assignmentId}:{signature}</c>.</param>
    Task<StaffCheckInResult> ScanStaffAsync(
        Guid scannedByUserId, Guid scanEventId, string pass, string? deviceInfo, CancellationToken ct = default);
}
