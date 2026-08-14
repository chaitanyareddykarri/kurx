namespace Kurx.Application.Abstractions;

/// <summary>The platform's one "the operation succeeded and there is nothing to return" response body.
///
/// <para>80 endpoints across 34 endpoint files answered <c>Results.Ok(new { ok = true })</c> — the same
/// two bytes of meaning, written 80 times as an anonymous type, so the published contract described the
/// body of none of them. This is that shape, named once. It is deliberately <b>not</b> per-endpoint
/// (<c>DeleteEventSuccessResponse</c>, <c>PublishEventSuccessResponse</c>, …): those would be 80 spellings
/// of one contract and a client would have to learn each.</para>
///
/// <para><b>Why this is not simply <c>204 No Content</c>.</b> These endpoints already ship a body, and
/// three clients parse it. Dropping it would be an API change; this names the shape that exists rather
/// than redesigning it. New endpoints with nothing to say should prefer 204.</para>
///
/// <para>Lives in this namespace because <c>SnakeCaseResponseConverter</c> selects response types by
/// namespace: a copy of this record declared beside an endpoint would serialize camelCase and silently
/// change the wire. <c>Ok</c> snake-cases to <c>ok</c>, which is byte-identical to the anonymous shape it
/// replaces.</para></summary>
public sealed record OperationAck(bool Ok)
{
    /// <summary>The only value this type takes on the wire — every call site answered a literal
    /// <c>true</c>. Shared so 80 endpoints do not each allocate an identical instance.</summary>
    public static readonly OperationAck Success = new(true);
}
