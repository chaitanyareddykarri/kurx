namespace Kurx.Application.Abstractions;

/// <summary>Wire shapes for the authentication and registration surface (D-313).
///
/// <para>A sibling of <c>EndpointResponses.cs</c> rather than more entries inside it: that file was being
/// edited concurrently while these were written, and the only property that actually matters here is the
/// <b>namespace</b> — <c>SnakeCaseResponseConverter</c> selects response types by it, so a record declared
/// anywhere else in the project would silently serialize camelCase and change the wire. Same namespace,
/// same behaviour, no contention over one file.</para>
///
/// <para>Each record reproduces exactly what its handler already emitted; the handlers now construct the
/// record instead of an anonymous object, so the compiler — not a comment — is what keeps the declared
/// contract and the real body in agreement.</para></summary>
public sealed record RegistrationStatusResponse(
    bool HasPassword,
    string? Email,
    bool EmailVerified,
    /// <summary>Canonical E.164 where the D-089 backfill has reached this row, legacy digits otherwise.</summary>
    string? Phone,
    bool PhoneVerified,
    bool HasTrustedDevice,
    /// <summary>The blocking flag (D-311): profile complete AND a password set. Deliberately narrower
    /// than <see cref="Remaining"/>, which also lists the steps that are offered but never gate.</summary>
    bool NeedsOnboarding,
    /// <summary>Outstanding ceremony steps in the order the client walks them:
    /// <c>complete_profile</c> → <c>create_password</c> → <c>verify_email</c> → <c>enroll_device</c>.</summary>
    IReadOnlyList<string> Remaining);

/// <summary>Answer to a completed email-verification challenge.
///
/// <para>Carries <c>ok</c> <i>and</i> <c>email_verified</c> because that is what the endpoint has always
/// sent and clients bind both. It is deliberately not collapsed to <see cref="OperationAck"/>: the second
/// field states the resulting account fact, not merely that the call succeeded, and dropping it would be
/// a silent breaking change no compiler would catch.</para></summary>
public sealed record EmailVerificationResult(bool Ok, bool EmailVerified)
{
    /// <summary>The only value the success path produces — the endpoint returns this or an error.</summary>
    public static readonly EmailVerificationResult Verified = new(true, true);
}

/// <summary>A freshly-issued step-up challenge (D-181).
///
/// <para><c>match_number</c> is the anti-push-fatigue defence: the approving device shows a number the
/// user must confirm matches the one on screen, so an approval cannot be harvested by spamming prompts.
/// It is a challenge parameter, not a secret — the signature is what authenticates.</para></summary>
public sealed record StepUpChallengeResponse(
    Guid ChallengeId,
    string Nonce,
    int MatchNumber,
    DateTime ExpiresAt);

/// <summary>Whether the caller currently holds a satisfied step-up, and whether they could obtain one.
///
/// <para><c>can_step_up</c> is separate from <c>satisfied</c> on purpose: a user with no enrolled device
/// can never satisfy a step-up, and a client needs to tell "not yet" from "not possible" so it can offer
/// enrolment rather than a challenge that would go unanswered.</para></summary>
public sealed record StepUpStatusResponse(
    bool Satisfied,
    DateTime? ValidUntil,
    bool CanStepUp);

/// <summary>A freshly generated set of recovery codes — <b>the only time they are ever readable</b>.
///
/// <para>Only their hashes are stored, so this response is not repeatable: re-issuing replaces the set
/// rather than re-showing it. Carries <c>count</c> alongside the list because the client's
/// "write these down" screen states how many there are without counting the array itself.</para></summary>
public sealed record RecoveryCodesIssued(IReadOnlyList<string> Codes, int Count);

/// <summary>How many recovery codes remain unused. The codes themselves are never returned here — this
/// is the only recovery-code fact a client may read after issuance.</summary>
public sealed record RecoveryCodesRemaining(int Remaining);
