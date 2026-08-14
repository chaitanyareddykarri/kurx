namespace Kurx.Application.Abstractions;

/// <summary>The event registration → admission → credential layer (V3 §7, Phase 8) as an additive dual-write
/// shadow of the authoritative Order/Ticket. (Distinct from the auth-ceremony <c>IRegistrationService</c>.) One
/// <c>RegistrationPolicy</c> per ticket type (the Pass analog) holds the five axes; <c>MirrorOrderAsync</c>
/// projects registrations/admissions/credentials from a committed order — post-commit, never inline (§17.1
/// "admission side effects publish through the outbox, never inline") — so the shadow can never break the money
/// path. Order/Ticket stay authoritative; the cut-over is Phase 9. Management authz reuses the Phase-6
/// event-permission union — no parallel model.</summary>
public interface IEventRegistrationService
{
    /// <summary>Ensure the ticket type's registration policy exists and mirror the axes derivable from its
    /// current config (subject, payment, identity, sale windows). Added/updated on the caller's DbContext.</summary>
    Task SyncPolicyAsync(Guid ticketTypeId, CancellationToken ct = default);

    /// <summary>Ensure the ticket type's <see cref="Pass"/> (V3 §9.2, the commercial product) and its single
    /// <c>AdmissionRight(SINGLE)</c> exist and mirror the ticket type's commercial fields (name, price, quantity,
    /// sale window, per-subject limit). Phase 9 Option A: one Pass per ticket type (1:1). Added/updated on the
    /// caller's DbContext, so it commits atomically with the ticket type change.</summary>
    Task SyncPassAsync(Guid ticketTypeId, CancellationToken ct = default);

    Task<ServiceResult<RegistrationPolicyView>> GetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, CancellationToken ct = default);
    Task<ServiceResult<RegistrationPolicyView>> SetPolicyAsync(Guid actorId, Guid eventId, Guid ticketTypeId, bool isAdmin, RegistrationPolicyInput input, CancellationToken ct = default);

    /// <summary>Idempotently project the registration + admissions + credentials + VAR for an order from its
    /// current tickets, on its OWN isolated DbContext (a fresh scope). Used out-of-band by reconciliation /
    /// repair / backfill; a failure there never affects a committed order.</summary>
    Task MirrorOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>The <b>authoritative, in-transaction</b> projection (V3 §6.1/§17.1, Phase 9): produces the
    /// registration + admissions + credentials + VAR on the <i>caller's</i> DbContext, so they commit atomically
    /// in the same money transaction as the order/ticket and the conditional decrement — the admission is never
    /// eventually-consistent. Same idempotent projection engine as <see cref="MirrorOrderAsync"/>; the only
    /// difference is it runs on the shared request context instead of a fresh scope.</summary>
    Task ProjectOrderInTransactionAsync(Guid orderId, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<RegistrationView>>> GetForEventAsync(Guid actorId, Guid eventId, bool isAdmin, CancellationToken ct = default);

    /// <summary>§17.1 reconciliation, Phase-8 form: proves the shadow matches the authority per event —
    /// registrations == orders, active admissions == active tickets, and every active account-holder admission
    /// carries a credential. Read-only; returns only drifting events. Use <see cref="RepairAsync"/> to heal.</summary>
    Task<IReadOnlyList<RegistrationDrift>> ReconcileAsync(Guid? eventId = null, CancellationToken ct = default);

    /// <summary>Self-heal (review P3): re-project every order in a drifting event (and any order missing a
    /// registration) so a missed/failed/partial projection is repaired without waiting for a restart backfill.
    /// Re-projection is idempotent and also relinks credentials. Returns the number of orders re-projected.</summary>
    Task<int> RepairAsync(Guid? eventId = null, CancellationToken ct = default);

    /// <summary>One-time, idempotent backfill: a policy per ticket type and the registration chain for every
    /// existing order. Runs at startup.</summary>
    Task<int> BackfillAsync(CancellationToken ct = default);
}

public record RegistrationPolicyInput(string? Subject, IReadOnlyList<string>? Gates, string? IdentityRequirement,
    string? Allocation, string? Payment, DateTime? OpensAt, DateTime? ClosesAt, int? LateWindowMinutes,
    DateTime? EditUntil, DateTime? CancelUntil, IReadOnlyList<string>? DocumentsRequired);

public record RegistrationPolicyView(Guid EventId, Guid TicketTypeId, string Subject, IReadOnlyList<string> Gates,
    string IdentityRequirement, string Allocation, string Payment, DateTime? OpensAt, DateTime? ClosesAt,
    int? LateWindowMinutes, DateTime? EditUntil, DateTime? CancelUntil, IReadOnlyList<string> DocumentsRequired);

public record RegistrationView(Guid Id, Guid EventId, Guid TicketTypeId, string SubjectType, Guid? SubjectId,
    Guid OrderId, string State, int AdmissionCount, DateTime CreatedAt);

public record RegistrationDrift(Guid EventId, int Orders, int Registrations, int ActiveTickets, int ActiveAdmissions,
    int AdmissionsMissingCredential);
