using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Providers;

/// <summary>
/// The default <see cref="IFileScanner"/>: always reports <see cref="FileScanResult.Clean"/>.
///
/// **This provides no malware protection whatsoever.** It exists so the attachment pipeline has a
/// real scanner call site from day one — every validation, audit and rejection path around it is
/// live and tested, so wiring a real scanner later is configuration, not surgery.
///
/// A real scanner (ClamAV, or a cloud scanning service) is a **deployment requirement before
/// unrestricted attendee uploads are enabled in production**. Recorded in D-110 and in
/// docs/security/chat-security.md.
/// </summary>
public class NoOpFileScanner(ILogger<NoOpFileScanner> log) : IFileScanner
{
    private bool _warned;

    public Task<FileScanResult> ScanAsync(string storageKey, CancellationToken ct = default)
    {
        // Once per process: loud enough to notice in a deployment, quiet enough not to spam a dev log.
        if (!_warned)
        {
            _warned = true;
            log.LogWarning(
                "NoOpFileScanner is active — uploaded files are NOT scanned for malware. "
                + "Configure a real FILE_SCANNER provider before enabling attendee uploads in production.");
        }
        return Task.FromResult(FileScanResult.Clean);
    }
}
