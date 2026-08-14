using System.Text.Json;
using System.Text.Json.Serialization;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;

namespace Kurx.Infrastructure.Audit;

/// <summary>
/// D-102 (M3a): stages a structured audit row on the current unit of work. The existing <c>audit_log</c>
/// table is reused unchanged (expand-then-contract) — the discipline lands in the <c>DetailsJson</c>
/// envelope now, and M3b migrates the envelope's fields into real columns once the shape has settled.
/// </summary>
public class AuditWriter(KurxDbContext db, ICorrelationAccessor correlation) : IAuditWriter
{
    /// <summary>Envelope schema version — bump when the shape changes so readers can branch.</summary>
    public const int EnvelopeVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Write(AuditEvent auditEvent)
        => db.AuditLogs.Add(new AuditLog
        {
            ActorType = auditEvent.ActorType,
            ActorId = auditEvent.ActorId,
            Action = auditEvent.Action,
            Entity = auditEvent.SubjectType,
            EntityId = auditEvent.SubjectId,
            DetailsJson = Envelope(auditEvent, correlation.CorrelationId),
        });

    /// <summary>Serializes the audit envelope. Exposed for tests so the on-disk shape is pinned.</summary>
    public static string Envelope(AuditEvent auditEvent, string? correlationId)
        => JsonSerializer.Serialize(new
        {
            v = EnvelopeVersion,
            correlation_id = correlationId,
            before = auditEvent.Before,
            after = auditEvent.After,
        }, Options);
}

/// <summary>Default accessor for background jobs and tests, where there is no HTTP request in scope.
/// The Api layer registers an HttpContext-backed implementation that overrides this.</summary>
public class NullCorrelationAccessor : ICorrelationAccessor
{
    public string? CorrelationId => null;
}
