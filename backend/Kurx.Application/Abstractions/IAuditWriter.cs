namespace Kurx.Application.Abstractions;

/// <summary>
/// D-102 (M3a): one typed audit event, the vocabulary every subsystem writes through. It replaces the
/// ad-hoc <c>DetailsJson</c> strings each service used to hand-format, so an auditor can read the trail
/// without knowing which service wrote a row — and so M4 (money) and M6 (media/evidence) can be designed
/// on a stable shape rather than inventing their own.
/// </summary>
/// <param name="Action">Dotted, past-tense-ish verb: <c>event.status_change</c>, <c>org.verification.approve</c>.</param>
/// <param name="SubjectType">The table/aggregate the action happened to, e.g. <c>events</c>, <c>organizations</c>.</param>
/// <param name="SubjectId">Primary key of that subject.</param>
/// <param name="ActorType"><c>user</c>, <c>admin</c>, or <c>system</c> (background jobs, webhooks).</param>
/// <param name="ActorId">Null for system actors.</param>
/// <param name="Before">State before the change. Must never carry PII (D-102 redaction rule).</param>
/// <param name="After">State after the change. Must never carry PII.</param>
public sealed record AuditEvent(
    string Action,
    string SubjectType,
    Guid SubjectId,
    string ActorType = "user",
    Guid? ActorId = null,
    object? Before = null,
    object? After = null);

/// <summary>
/// Writes audit events. Deliberately <b>synchronous and non-saving</b>: it stages the row on the caller's
/// unit of work so the audit entry commits in the SAME transaction as the change it describes. An audit
/// trail that can commit separately from its subject is not an audit trail.
/// </summary>
public interface IAuditWriter
{
    void Write(AuditEvent auditEvent);
}

/// <summary>
/// Supplies the current request's correlation id so an audit row can be joined to its logs and traces.
/// Infrastructure has no ASP.NET dependency, so the HTTP-backed implementation lives in the Api layer;
/// background jobs and tests resolve a null accessor and simply record no correlation id.
/// </summary>
public interface ICorrelationAccessor
{
    string? CorrelationId { get; }
}
