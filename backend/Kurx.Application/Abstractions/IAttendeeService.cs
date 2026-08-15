namespace Kurx.Application.Abstractions;

public record AttendeeRow(Guid TicketId, Guid Code, string State, DateTime? CheckedInAt, string BuyerName, string BuyerPhone,
    Guid TicketTypeId, string TicketTypeName, Guid? GroupId, int? GroupNumber, string? GroupDisplayName,
    IReadOnlyDictionary<string, string?> Answers,
    // Identity for the Professional Identity System (D-20x) — null when the ticket has no linked
    // account (guest checkout) or the account's profile isn't public. Buyer name/phone above are
    // organizer-only data shown regardless; these three are specifically what the public profile
    // link/Connect action needs, so they respect ProfilePublic even for the org's own attendee sheet.
    Guid? BuyerUserId, string? BuyerUsername, string? BuyerAvatarKey,
    /// <summary>Presigned companion to <c>BuyerAvatarKey</c> (D-302). Null when there is no key.</summary>
    string? BuyerAvatarUrl = null);

public record AttendeeListFilter(string? Q, Guid? TicketTypeId, Guid? GroupId, string? State, int Page, int PageSize);

/// <summary>The host "attendee sheet" — every custom-field answer as a column (spec section H4).</summary>
public interface IAttendeeService
{
    Task<ServiceResult<(IReadOnlyList<AttendeeRow> Items, int Total)>> ListAsync(Guid userId, Guid eventId, AttendeeListFilter filter, CancellationToken ct = default);

    /// <summary>Every ticket as CSV columns, written straight to <paramref name="output"/>.
    ///
    /// <para><b>Returns a writer rather than a <see cref="Stream"/> (DB-6).</b> The old shape materialised
    /// every ticket, hydrated all of them, built one <c>StringBuilder</c> and copied it into a
    /// <c>MemoryStream</c> — roughly three copies of the whole roster resident at once, and a big event is
    /// exactly when someone exports. Authorization is still resolved <b>eagerly</b>, before the delegate is
    /// ever invoked, so a refusal is still a clean 404/403 with no bytes written; only the body streams.</para></summary>
    Task<ServiceResult<Func<Stream, CancellationToken, Task>>> ExportCsvAsync(
        Guid userId, Guid eventId, CancellationToken ct = default);
}
