namespace Kurx.Application.Abstractions;

/// <summary>Walk-in registration (V3 §7.6, Phase 13) — a staff <see cref="EventParticipant"/> registers an attendee at
/// the gate, drawing from the <c>WalkIn</c>-segment InventoryPool, producing a Registration+Admission+Credential via
/// the <b>existing authoritative Order/Ticket projection</b> (the only minting path). Offline-safe via the Phase-9
/// per-caller idempotency key: the client queues locally and replays; a replay never creates a duplicate.</summary>
public interface IWalkInService
{
    Task<ServiceResult<WalkInView>> CreateAsync(Guid staffUserId, Guid eventId, bool isAdmin, WalkInInput input, CancellationToken ct = default);
}

public record WalkInInput(Guid TicketTypeId, string? GuestName, string? GuestPhone, string? GuestEmail, bool Free, string? IdempotencyKey);

public record WalkInView(Guid OrderId, Guid RegistrationId, Guid AdmissionId, Guid? CredentialId, Guid TicketCode, string? GuestAccessToken);

/// <summary>Delegated registration / SeatBlock (V3 §7.5, Phase 13) — an org unit reserves N seats before people are
/// known; the seats are bought through the <b>authoritative money path</b> and mint <b>unassigned</b> Admissions; the
/// delegate console binds people later, governed by deadline + reassign limit + a per-assignment audit. Payment is
/// data/authz only this phase (FREE | DEFERRED, §9.7). Organiser gates reuse <c>event:manage</c>; assignment is the
/// block's delegate (or organiser/admin).</summary>
public interface ISeatBlockService
{
    Task<ServiceResult<SeatBlockView>> CreateAsync(Guid actorId, Guid eventId, bool isAdmin, SeatBlockInput input, CancellationToken ct = default);
    Task<ServiceResult<SeatBlockView>> GetAsync(Guid actorId, Guid seatBlockId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<SeatBlockView>>> ListForEventAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<SeatView>>> ListSeatsAsync(Guid actorId, Guid seatBlockId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<SeatView>> AssignAsync(Guid actorId, Guid seatId, bool isAdmin, SeatAssignInput input, CancellationToken ct = default);
    Task<ServiceResult<SeatView>> UnassignAsync(Guid actorId, Guid seatId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<SeatBlockStatusView>> StatusAsync(Guid actorId, Guid seatBlockId, bool isAdmin, CancellationToken ct = default);
}

public record SeatBlockInput(Guid TicketTypeId, Guid RegistrantOrgUnitId, Guid? PayerId, Guid DelegateUserId,
    string? PaymentMode, int Quantity, DateTime? AssignmentDeadline, int? ReassignLimit);

public record SeatBlockView(Guid Id, Guid EventId, Guid TicketTypeId, Guid RegistrantOrgUnitId, Guid? PayerId,
    Guid DelegateUserId, string PaymentMode, int Quantity, DateTime? AssignmentDeadline, int ReassignLimit,
    Guid OrderId, string State, int AssignedCount);

public record SeatAssignInput(Guid PersonId, string? AnswersJson);

public record SeatView(Guid Id, Guid SeatBlockId, Guid AdmissionId, Guid? PersonId, int ReassignCount,
    DateTime? ReassignableUntil, DateTime? AssignedAt, string AdmissionState);

public record SeatBlockStatusView(int Total, int Assigned, int Unassigned, IReadOnlyList<Guid> IncompleteSeatIds);
