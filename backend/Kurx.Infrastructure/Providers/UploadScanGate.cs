using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Providers;

/// <summary>
/// The malware gate for every upload path outside chat and posts (D-338).
///
/// <para><b>Why this exists.</b> <see cref="IFileScanner"/> shipped real (D-298) but was injected into
/// exactly two services — <c>ChatService</c> and <c>PostService</c>. The other six presign paths
/// (event media, org-verification evidence, membership-claim evidence, representation-request evidence,
/// institutional-authorization evidence, and profile images) wrote bytes to storage and persisted the key
/// with no scan at any point. The unscanned set included every document a staff reviewer opens in the
/// admin console, which is the higher-consequence path: a chat attachment is opened by a peer, a
/// government-ID PDF is opened by an employee.</para>
///
/// <para><b>Why here and not in <c>PUT /v1/storage/{key}</c>.</b> That endpoint looks like the one choke
/// point every upload passes through, and today it is — but only because <c>LocalDiskStorage</c> presigns
/// back to our own API. An S3 presign uploads <i>directly to S3</i> and never touches this process, so a
/// gate there would silently stop covering anything the day the S3 adapter ships. A fix that quietly
/// un-fixes itself on an unrelated change is worse than no fix, because it also removes the reason to look
/// again. The scan therefore sits where the server first <b>claims</b> a key — the same place chat and
/// posts already put it, and the place that stays true for any storage provider.</para>
///
/// <para><b>Fails closed, like the scanner it wraps.</b> Only <see cref="FileScanResult.Clean"/> returns
/// null. <c>Infected</c>, <c>ScanFailed</c> and <c>Unsupported</c> all reject — a scanner that is down
/// blocks the claim rather than waving an unscanned object through. A missing object reaches the same
/// place: <c>ClamAvFileScanner</c> reads the bytes through <see cref="IStorage"/>, so an absent key throws
/// inside the scanner and comes back as <c>ScanFailed</c>.</para>
///
/// <para><b>Not applied to chat and posts.</b> Both already scan, and both return domain-specific error
/// codes (<c>file_infected</c>/<c>scan_unavailable</c>, <c>invalid_media</c>) that are part of a tested
/// client contract, plus cleanup of their own row. Rewriting two working, covered call sites to share this
/// helper would risk those contracts to buy symmetry — so they stay as they are, and this covers the six
/// that had nothing.</para>
/// </summary>
public sealed class UploadScanGate(IFileScanner scanner, IStorage storage, IAuditWriter audit)
{
    /// <summary>Scans one claimed key. Returns <c>null</c> when it may be persisted, or an error code the
    /// caller returns verbatim. A null/blank key is "nothing was claimed" and passes — the callers below
    /// treat an absent document as an existing validation concern, not this one's.</summary>
    public async Task<string?> RejectAsync(string? storageKey, Guid actorId, string subjectType, Guid subjectId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return null;

        var verdict = await scanner.ScanAsync(storageKey.Trim(), ct);
        if (verdict == FileScanResult.Clean) return null;

        // Staged on the caller's unit of work, so the rejection commits with whatever else that request
        // wrote — or with nothing, if the caller returns before saving. Either way it never commits
        // separately from the decision it records (D-102).
        audit.Write(new AuditEvent("upload.scan_rejected", subjectType, subjectId,
            ActorId: actorId,
            After: new { storage_key = storageKey, verdict = verdict.ToString() }));

        await TryDeleteAsync(storageKey.Trim(), ct);

        return verdict == FileScanResult.Infected ? "file_infected" : "scan_unavailable";
    }

    /// <summary>The same gate over a set of keys, for the evidence lists. Stops at the first rejection:
    /// the caller refuses the whole submission anyway, and continuing would scan documents that are about
    /// to be discarded.</summary>
    public async Task<string?> RejectAnyAsync(IEnumerable<string?> storageKeys, Guid actorId, string subjectType,
        Guid subjectId, CancellationToken ct = default)
    {
        foreach (var key in storageKeys)
            if (await RejectAsync(key, actorId, subjectType, subjectId, ct) is { } error)
                return error;

        return null;
    }

    /// <summary>Same shape and same reasoning as <c>ChatService.TryDeleteObjectAsync</c>: infected or
    /// unverifiable bytes must not stay reachable, but cleanup must never turn a clean refusal into a 500.
    /// Only <see cref="LocalDiskStorage"/> implements deletion today; <see cref="IStorage"/> has no
    /// <c>DeleteAsync</c>, so other providers gain this when they are built.</summary>
    private async Task TryDeleteAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            if (storage is LocalDiskStorage disk) await disk.DeleteAsync(storageKey, ct);
        }
        catch
        {
            // Never fail a user-facing operation because cleanup could not reach storage.
        }
    }
}
