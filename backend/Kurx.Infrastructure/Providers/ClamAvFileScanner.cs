using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Providers;

/// <summary>
/// Real malware scanning against a ClamAV daemon (D-298), speaking clamd's INSTREAM protocol directly
/// over TCP.
///
/// <para><b>Why clamd and not a cloud API.</b> Attachments are attendee-supplied files on an events
/// platform; shipping every one of them to a third party is a data-egress decision nobody asked for,
/// and it puts an external service on the upload path. clamd runs beside the API, needs no API key,
/// and its signature database updates independently of a deploy.</para>
///
/// <para><b>Why the raw protocol and not a client library.</b> INSTREAM is four lines of framing: a
/// command, length-prefixed chunks, a zero terminator, one reply. A dependency to write that would be
/// more code to audit than the code it replaces, and this sits directly on the security path.</para>
///
/// <para><b>Fails closed.</b> Every failure — refused connection, timeout, truncated reply, a file
/// larger than clamd's StreamMaxLength — returns <see cref="FileScanResult.ScanFailed"/>, never
/// <see cref="FileScanResult.Clean"/>. The caller rejects the upload on ScanFailed, so a scanner that
/// is down blocks uploads rather than silently waving malware through. That is the whole reason this
/// class exists, and it is the one behaviour that must never be "improved" into a fallback.</para>
/// </summary>
public sealed class ClamAvFileScanner(IStorage storage, ILogger<ClamAvFileScanner> log, ClamAvOptions options)
    : IFileScanner
{
    /// <summary>clamd's own default chunk ceiling is
    /// <c>StreamMaxLength</c>; 64 KiB frames sit well inside every deployment's limit and keep the
    /// socket write loop simple.</summary>
    private const int ChunkSize = 64 * 1024;

    public async Task<FileScanResult> ScanAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var content = await storage.GetAsync(storageKey, ct);
            return await ScanBytesAsync(content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;   // the request went away; not a scan verdict
        }
        catch (Exception ex)
        {
            // Deliberately broad: the contract says implementations must not throw, because a throwing
            // scanner is indistinguishable from a clean file to a caller that is not expecting one.
            log.LogError(ex, "ClamAV scan failed for {StorageKey}", storageKey);
            return FileScanResult.ScanFailed;
        }
    }

    private async Task<FileScanResult> ScanBytesAsync(byte[] content, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.Timeout);

        using var client = new TcpClient();
        await client.ConnectAsync(options.Host, options.Port, timeout.Token);
        await using var stream = client.GetStream();

        // zINSTREAM — the null-terminated form. The newline form (nINSTREAM) is also accepted, but the
        // z-form is what clamd documents as unambiguous when a reply is parsed by a machine.
        await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);

        var header = new byte[4];
        for (var offset = 0; offset < content.Length; offset += ChunkSize)
        {
            var size = Math.Min(ChunkSize, content.Length - offset);
            BinaryPrimitives.WriteInt32BigEndian(header, size);
            await stream.WriteAsync(header, timeout.Token);
            await stream.WriteAsync(content.AsMemory(offset, size), timeout.Token);
        }

        // A zero-length chunk ends the stream. Without it clamd waits for more data until it times out,
        // and the scan would report ScanFailed for a perfectly good file.
        BinaryPrimitives.WriteInt32BigEndian(header, 0);
        await stream.WriteAsync(header, timeout.Token);
        await stream.FlushAsync(timeout.Token);

        var reply = await ReadReplyAsync(stream, timeout.Token);

        // clamd answers one of: "stream: OK", "stream: <SigName> FOUND", or "... ERROR".
        if (reply.EndsWith("OK", StringComparison.Ordinal)) return FileScanResult.Clean;
        if (reply.EndsWith("FOUND", StringComparison.Ordinal))
        {
            // The signature name is operationally useful and contains no user content, so it is safe
            // to log. The storage key is logged by the caller.
            log.LogWarning("ClamAV reported an infected upload: {Reply}", reply);
            return FileScanResult.Infected;
        }

        log.LogError("ClamAV returned an unrecognised reply: {Reply}", reply);
        return FileScanResult.ScanFailed;
    }

    private static async Task<string> ReadReplyAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[512];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (read == 0) break;                      // clamd closed the connection: reply complete
            total += read;
            if (buffer[total - 1] == 0) break;         // z-form replies are null-terminated
        }
        return Encoding.ASCII.GetString(buffer, 0, total).TrimEnd('\0', '\n', ' ');
    }
}

/// <summary>Where clamd lives and how long to wait for it. Read once at registration.</summary>
public sealed record ClamAvOptions(string Host, int Port, TimeSpan Timeout);
