namespace Kurx.Application.Abstractions;

/// <summary>
/// Getting certificates to the people they were issued to (D-355, Phase 8).
///
/// <para><b>Queue, then drain.</b> Sending is never done inside the request that asks for it. A send loop
/// over four hundred recipients inside an HTTP call fails halfway with no record of how far it got, and
/// the organiser's only recourse is to press the button again and double-send to everyone who already
/// received one. Instead each intended send becomes a row, and a background job drains them — so "who has
/// been sent what" is a query rather than a guess.</para>
///
/// <para><b>Sent never means delivered.</b> <c>CertificateDeliveryStatus.Sent</c> records that the email
/// provider ACCEPTED the message. Without a bounce pipeline there is no state that can honestly claim it
/// reached anyone, so none is offered and no wording anywhere may imply it.</para>
/// </summary>
public interface ICertificateDeliveryService
{
    /// <summary>Queues a send for every certificate in a run that has somewhere to go.
    ///
    /// <para>Idempotent: a certificate already queued or already sent is skipped, so pressing the button
    /// twice does not send two copies. Re-sending deliberately is <see cref="ResendAsync"/>.</para></summary>
    Task<ServiceResult<CertificateDeliverySummary>> QueueBatchAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Queues one certificate, optionally to a corrected address. Always creates a new attempt,
    /// because a deliberate resend is exactly the case where "already sent" must not block it.</summary>
    Task<ServiceResult<CertificateDeliveryView>> ResendAsync(
        Guid userId, Guid certificateId, string? destination, bool isAdmin, CancellationToken ct = default);

    /// <summary>What happened to a run's sends. The honest counts, including the ones that failed.</summary>
    Task<ServiceResult<CertificateDeliverySummary>> SummariseBatchAsync(
        Guid userId, Guid batchId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Sends what is queued. Called by the background job, never from a request.</summary>
    Task<CertificateDispatchResult> DispatchPendingAsync(int max, CancellationToken ct = default);
}

/// <param name="Pending">Queued and not yet attempted, or awaiting a retry.</param>
/// <param name="Sent">The provider accepted it. NOT a claim that anyone received it.</param>
/// <param name="Failed">Gave up after repeated attempts, or could not be attempted at all.</param>
/// <param name="NoDestination">Certificates with no email address and no linked account. Counted and
/// surfaced rather than silently ignored — "we sent 396 of your 400" is the organiser's problem to solve,
/// and they can only solve it if they are told.</param>
public sealed record CertificateDeliverySummary(
    Guid BatchId,
    int Total,
    int Pending,
    int Sent,
    int Failed,
    int NoDestination,
    IReadOnlyList<CertificateDeliveryView> Recent);

public sealed record CertificateDeliveryView(
    Guid Id,
    Guid CertificateId,
    string Channel,
    string? Destination,
    string Status,
    string? Error,
    int AttemptCount,
    DateTime? SentAt,
    DateTime CreatedAt);

public sealed record CertificateDispatchResult(int Sent, int Failed, int Skipped);
