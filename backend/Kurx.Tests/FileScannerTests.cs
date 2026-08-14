using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure;
using Kurx.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kurx.Tests;

/// <summary>
/// D-298 — the ClamAV scanner, against a fake clamd.
///
/// A unit test rather than an integration one, deliberately: the behaviour that matters is the wire
/// protocol and what happens when the daemon misbehaves, and a real clamd can only demonstrate the
/// happy path. Standing up a socket lets the tests assert the cases production actually fails on — a
/// refused connection, a hang, a truncated reply — none of which a healthy daemon will ever produce
/// on demand.
///
/// <b>The load-bearing case is fail-closed.</b> Every failure must be ScanFailed, never Clean: the
/// upload path rejects on ScanFailed, so a scanner that is down blocks uploads instead of silently
/// waving malware through. If one of these ever "passes" by returning Clean, the protection is gone
/// and nothing else in the system will notice.
/// </summary>
public class FileScannerTests
{
    /// <summary>The EICAR test string — the industry-standard harmless file every scanner reports as
    /// infected. Split so this source file is not itself flagged by a scanner reading the repo.</summary>
    private static readonly byte[] Eicar = Encoding.ASCII.GetBytes(
        @"X5O!P%@AP[4\PZX54(P^)7CC)7}$" + "EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    private sealed class StubStorage(byte[] content) : IStorage
    {
        public Task<byte[]> GetAsync(string key, CancellationToken ct = default) => Task.FromResult(content);
        public Task<PresignedUpload> PresignPutAsync(string k, string c, long m, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> PresignGetAsync(string k, TimeSpan? t = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task PutAsync(string k, byte[] c, string t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string k, CancellationToken ct = default) => Task.FromResult(true);
    }

    /// <summary>A fake clamd: accepts one connection, drains the INSTREAM frames, replies. Returns the
    /// port and the bytes it received, so a test can assert the framing as well as the verdict.</summary>
    private static (int Port, Task<byte[]> Received) StartFakeClamd(string reply, bool hangUpEarly = false)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var received = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var body = new MemoryStream();

            // "zINSTREAM\0" then length-prefixed chunks until a zero-length one.
            var command = new byte[10];
            await stream.ReadExactlyAsync(command);

            if (hangUpEarly)
            {
                listener.Stop();
                return body.ToArray();
            }

            var header = new byte[4];
            while (true)
            {
                await stream.ReadExactlyAsync(header);
                var size = BinaryPrimitives.ReadInt32BigEndian(header);
                if (size == 0) break;
                var chunk = new byte[size];
                await stream.ReadExactlyAsync(chunk);
                body.Write(chunk);
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes(reply));
            await stream.FlushAsync();
            listener.Stop();
            return body.ToArray();
        });

        return (port, received);
    }

    private static ClamAvFileScanner Scanner(byte[] content, int port, int timeoutSeconds = 10) =>
        new(new StubStorage(content), NullLogger<ClamAvFileScanner>.Instance,
            new ClamAvOptions("127.0.0.1", port, TimeSpan.FromSeconds(timeoutSeconds)));

    [Fact]
    public async Task A_clean_file_is_reported_clean()
    {
        var (port, _) = StartFakeClamd("stream: OK\0");

        var verdict = await Scanner("harmless"u8.ToArray(), port).ScanAsync("chat/x/file.txt");

        Assert.Equal(FileScanResult.Clean, verdict);
    }

    [Fact]
    public async Task An_infected_file_is_reported_infected()
    {
        var (port, _) = StartFakeClamd("stream: Eicar-Signature FOUND\0");

        var verdict = await Scanner(Eicar, port).ScanAsync("chat/x/eicar.com");

        Assert.Equal(FileScanResult.Infected, verdict);
    }

    [Fact]
    public async Task The_whole_file_reaches_the_daemon_intact_across_chunk_boundaries()
    {
        // 150 KiB spans three 64 KiB frames. An off-by-one in the framing loop shows up here and
        // nowhere else — a small file fits one chunk and passes whatever the loop does.
        var content = new byte[150 * 1024];
        Random.Shared.NextBytes(content);
        var (port, received) = StartFakeClamd("stream: OK\0");

        var verdict = await Scanner(content, port).ScanAsync("chat/x/big.bin");

        Assert.Equal(FileScanResult.Clean, verdict);
        Assert.Equal(content, await received);
    }

    [Fact]
    public async Task An_unreachable_daemon_fails_closed()
    {
        // Bind and immediately release, so the port is almost certainly refusing connections.
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var deadPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var verdict = await Scanner("anything"u8.ToArray(), deadPort).ScanAsync("chat/x/file.txt");

        // NOT Clean. The upload path rejects on ScanFailed, so this is what stops an unscanned file
        // from reaching a room while clamd is down.
        Assert.Equal(FileScanResult.ScanFailed, verdict);
    }

    [Fact]
    public async Task A_daemon_that_hangs_up_without_replying_fails_closed()
    {
        var (port, _) = StartFakeClamd("", hangUpEarly: true);

        var verdict = await Scanner("anything"u8.ToArray(), port).ScanAsync("chat/x/file.txt");

        Assert.Equal(FileScanResult.ScanFailed, verdict);
    }

    [Fact]
    public async Task An_unrecognised_reply_fails_closed_rather_than_being_read_as_clean()
    {
        // clamd answers ERROR when a file exceeds StreamMaxLength, among other things. Anything that
        // is not an explicit OK must not be treated as one.
        var (port, _) = StartFakeClamd("INSTREAM size limit exceeded. ERROR\0");

        var verdict = await Scanner("anything"u8.ToArray(), port).ScanAsync("chat/x/file.txt");

        Assert.Equal(FileScanResult.ScanFailed, verdict);
    }

    [Fact]
    public async Task A_daemon_that_never_answers_times_out_and_fails_closed()
    {
        // Accepts the connection and then says nothing at all — the failure a plain port check cannot
        // see, and the one a naive client waits on forever.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = Task.Run(async () => { using var c = await listener.AcceptTcpClientAsync(); await Task.Delay(5000); });

        var verdict = await Scanner("anything"u8.ToArray(), port, timeoutSeconds: 1)
            .ScanAsync("chat/x/file.txt");

        listener.Stop();
        Assert.Equal(FileScanResult.ScanFailed, verdict);
    }

    // ── Against a real daemon ──────────────────────────────────────────────────────────────────
    //
    // These skip themselves when clamd is not reachable, so CI and a laptop without it stay green,
    // and they run for real wherever it exists:
    //     docker compose --profile scanning up -d clamav
    //
    // Worth having despite the fake-clamd tests above: only a real daemon proves the framing matches
    // what ClamAV actually parses, and only a real signature database proves a known-bad file is
    // recognised rather than merely echoed back by a socket this test wrote itself.

    private static async Task<bool> ClamdIsUpAsync(int port)
    {
        try
        {
            using var probe = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await probe.ConnectAsync(IPAddress.Loopback, port, cts.Token);
            return true;
        }
        catch { return false; }
    }

    private const int RealClamdPort = 3310;

    [Fact]
    public async Task Real_clamd_reports_the_EICAR_test_file_as_infected()
    {
        if (!await ClamdIsUpAsync(RealClamdPort)) return;   // clamd not running: nothing to prove

        var verdict = await Scanner(Eicar, RealClamdPort, timeoutSeconds: 60)
            .ScanAsync("chat/x/eicar.com");

        Assert.Equal(FileScanResult.Infected, verdict);
    }

    [Fact]
    public async Task Real_clamd_reports_an_ordinary_file_as_clean()
    {
        if (!await ClamdIsUpAsync(RealClamdPort)) return;

        var verdict = await Scanner("just some meeting notes"u8.ToArray(), RealClamdPort, timeoutSeconds: 60)
            .ScanAsync("chat/x/notes.txt");

        Assert.Equal(FileScanResult.Clean, verdict);
    }

    // ── The registration switch ────────────────────────────────────────────────────────────────
    //
    // Asserted on the ServiceCollection rather than a built provider: what is under test is which
    // implementation the config string SELECTS, and resolving it would drag in a database, Hangfire
    // and every other registration for no extra information. This is the piece a live boot would
    // otherwise be the only check on.

    private static IConfiguration ConfigWith(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value))
                .Append(new("ConnectionStrings:Default", "Host=localhost;Database=x;Username=u;Password=p")))
            .Build();

    private static Type? RegisteredScanner(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKurxInfrastructure(config);
        return services.Last(d => d.ServiceType == typeof(IFileScanner)).ImplementationType;
    }

    [Fact]
    public void The_default_configuration_registers_the_no_op_scanner()
    {
        Assert.Equal(typeof(NoOpFileScanner), RegisteredScanner(ConfigWith()));
    }

    [Fact]
    public void FILE_SCANNER_clamav_registers_the_real_scanner()
    {
        Assert.Equal(typeof(ClamAvFileScanner), RegisteredScanner(ConfigWith(("FILE_SCANNER", "clamav"))));
    }

    [Fact]
    public void An_unknown_FILE_SCANNER_value_refuses_to_start_rather_than_falling_back_to_no_op()
    {
        // A typo must not silently downgrade a production deployment to no scanning at all. Failing at
        // startup is loud; a quiet fallback would be indistinguishable from working.
        var ex = Assert.Throws<NotSupportedException>(
            () => RegisteredScanner(ConfigWith(("FILE_SCANNER", "clamvav"))));
        Assert.Contains("clamav", ex.Message);
    }

    [Fact]
    public async Task The_no_op_scanner_still_reports_clean_so_development_is_unaffected()
    {
        // The default. It provides no protection by design — asserted so nobody "fixes" it into
        // something that fails closed and breaks every dev machine.
        var verdict = await new NoOpFileScanner(NullLogger<NoOpFileScanner>.Instance)
            .ScanAsync("chat/x/file.txt");

        Assert.Equal(FileScanResult.Clean, verdict);
    }
}
