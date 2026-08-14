namespace Kurx.Application.Abstractions;

/// <summary>Authentication telemetry (AM10, D-100). Lives in Application so Infrastructure services
/// can be instrumented without taking a dependency on the OpenTelemetry SDK — the same provider
/// boundary every other cross-cutting concern uses.
///
/// <para><b>Nothing here may carry a secret or PII.</b> Spans and metrics are exported to a
/// third-party backend and are readable by anyone with dashboard access, so the vocabulary is
/// deliberately limited to identifiers and outcomes: never a phone number, an OTP, a nonce, a token,
/// or a signature. `userId` is a GUID, which is already the least-identifying handle we have.</para></summary>
public interface IAuthTelemetry
{
    /// <summary>Starts a span for an authentication operation. Dispose ends it.
    /// <paramref name="operation"/> is a stable low-cardinality name (`login.start`,
    /// `passkey.register`, …) — never anything user-supplied, which would explode cardinality and
    /// can cost real money on a metered backend.</summary>
    IDisposable StartOperation(string operation, Guid? userId = null);

    /// <summary>Records the outcome of an auth attempt. <paramref name="method"/> is the rail
    /// (`otp`, `device`, `passkey`, `recovery`), <paramref name="outcome"/> the result
    /// (`success`, `denied`, `invalid`, `expired`).</summary>
    void RecordAuthAttempt(string method, string outcome);

    /// <summary>Records a security-relevant event for alerting (`risk.denied`,
    /// `refresh.reuse_detected`, `signing_key.compromised`). Severity mirrors `security_events`.</summary>
    void RecordSecurityEvent(string type, string severity);

    /// <summary>Marks the current span as failed with a low-cardinality reason. The reason must be
    /// an error <i>code</i>, never an exception message — messages carry internals and vary
    /// unboundedly.</summary>
    void RecordFailure(string reason);
}
