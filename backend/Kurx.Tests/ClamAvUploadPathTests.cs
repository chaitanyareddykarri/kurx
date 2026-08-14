using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Points the API at a REAL clamd over TCP (D-298).
///
/// <para>`FILE_SCANNER` is absent from <see cref="KurxApiFactory"/>, so every other class in this suite
/// runs on <c>NoOpFileScanner</c> — which reports Clean without looking at anything. That makes the whole
/// attachment suite silent about the scanner, and it is why <c>ClamAvFileScanner</c> shipped with only
/// unit coverage against a fake socket.</para></summary>
public class RealClamAvFactory : KurxApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("FILE_SCANNER", "clamav");
        builder.UseSetting("CLAMAV_HOST", Environment.GetEnvironmentVariable("CLAMAV_HOST") ?? "localhost");
        builder.UseSetting("CLAMAV_PORT", Environment.GetEnvironmentVariable("CLAMAV_PORT") ?? "3310");
        builder.UseSetting("CLAMAV_TIMEOUT_SECONDS", "60");
    }
}

/// <summary>The same wiring, aimed at a port nothing listens on — "the scanner is configured and the
/// daemon is gone", which is the operational condition that has to fail CLOSED.</summary>
public class UnreachableClamAvFactory : KurxApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("FILE_SCANNER", "clamav");
        builder.UseSetting("CLAMAV_HOST", "127.0.0.1");
        builder.UseSetting("CLAMAV_PORT", "1");      // nothing listens here
        builder.UseSetting("CLAMAV_TIMEOUT_SECONDS", "5");
    }
}

/// <summary>Shared room fixture for both classes below. Mirrors ChatAttachmentTests' setup, because the
/// path under test is the same one — presign, PUT, confirm — with a real scanner behind it.</summary>
public abstract class ClamAvUploadTestBase
{
    protected Guid RoomId, MemberId;

    /// <summary>The industry-standard harmless antivirus test signature. Not malware: it is a plain ASCII
    /// string every scanner recognises by agreement, which is exactly why it is the only safe way to prove
    /// a real engine is looking at real bytes. Assembled in pieces so this source file is not itself
    /// flagged by a scanner reading the repository.</summary>
    protected static byte[] Eicar() => Encoding.ASCII.GetBytes(
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$" + "EICAR-STANDARD-ANTIVIRUS-TEST-FILE" + "!$H+H*");

    protected static byte[] CleanText() => "quarterly notes, nothing interesting\n"u8.ToArray();

    protected void SeedRoom(KurxApiFactory factory, string phonePrefix, string slug)
    {
        factory.ResetDatabase();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();

        var host = new User { Name = "Scan Host", Phone = $"91{phonePrefix}1" };
        var member = new User { Name = "Scan Member", Phone = $"91{phonePrefix}2" };
        db.Users.AddRange(host, member);
        var category = new EventCategory { Level = CategoryLevel.Category, Name = "Scan", Slug = slug };
        db.EventCategories.Add(category);
        db.SaveChanges();

        MemberId = member.Id;
        var orgId = factory.SeedVerifiedOrg(host.Id, "Scan Org " + slug);
        var ev = new Event
        {
            RepresentingOrgId = orgId, CreatedBy = host.Id, CategoryId = category.Id,
            Title = "Scan Event", Slug = slug + "-event", ShortCode = slug[..3].ToUpperInvariant() + "901",
            Description = "d", VenueName = "v",
            StartsAt = DateTime.UtcNow.AddDays(3), EndsAt = DateTime.UtcNow.AddDays(4),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        db.SaveChanges();

        var room = new ChatRoom { EventId = ev.Id, Kind = ChatRoomKind.General };
        db.ChatRooms.Add(room);
        db.ChatMembers.AddRange(
            new ChatMember { RoomId = room.Id, UserId = host.Id, Role = ChatMemberRole.Host },
            new ChatMember { RoomId = room.Id, UserId = member.Id, Role = ChatMemberRole.Member });
        db.SaveChanges();
        RoomId = room.Id;
    }

    /// <summary>The production flow, end to end: presign → PUT the bytes → confirm. Confirm is where the
    /// scanner runs, so nothing here reaches into the scanner directly.</summary>
    protected async Task<(ServiceResult<ChatAttachmentView> Result, string Key)> UploadAsync(
        IServiceScope scope, byte[] bytes, string contentType, string fileName)
    {
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var ticket = await chat.PresignAttachmentAsync(RoomId, MemberId, fileName, contentType, bytes.Length);
        Assert.True(ticket.Ok, ticket.Error);

        await storage.PutAsync(ticket.Value!.Key, bytes, contentType);
        return (await chat.ConfirmAttachmentAsync(RoomId, MemberId, ticket.Value.Key), ticket.Value.Key);
    }
}

/// <summary>D-298 runtime verification against a live ClamAV daemon.</summary>
public class ClamAvUploadPathTests : ClamAvUploadTestBase, IClassFixture<RealClamAvFactory>
{
    private readonly RealClamAvFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static Guid _roomId, _memberId;

    public ClamAvUploadPathTests(RealClamAvFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { SeedRoom(factory, "98100440", "clamav-real"); _roomId = RoomId; _memberId = MemberId; _reset = true; }
        }
        RoomId = _roomId; MemberId = _memberId;
    }

    [Fact]
    public void The_container_resolves_the_real_scanner_not_the_no_op()
    {
        using var scope = _factory.Services.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IFileScanner>();
        // Without this, every assertion below would pass on NoOpFileScanner and prove nothing at all.
        Assert.Equal("ClamAvFileScanner", scanner.GetType().Name);
    }

    [Fact]
    public async Task A_clean_file_is_scanned_by_real_clamd_and_accepted()
    {
        using var scope = _factory.Services.CreateScope();
        var (result, key) = await UploadAsync(scope, CleanText(), "text/plain", "notes.txt");

        Assert.True(result.Ok, result.Error);
        Assert.Equal("text/plain", result.Value!.ContentType);

        // The object survives, and the row exists — a clean verdict is the only path that persists either.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.True(await db.ChatAttachments.AnyAsync(a => a.StorageKey == key));
        Assert.True(await scope.ServiceProvider.GetRequiredService<IStorage>().ExistsAsync(key));
    }

    [Fact]
    public async Task The_eicar_signature_is_rejected_as_infected_and_never_becomes_an_attachment()
    {
        using var scope = _factory.Services.CreateScope();
        var (result, key) = await UploadAsync(scope, Eicar(), "text/plain", "harmless.txt");

        Assert.False(result.Ok);
        Assert.Equal("file_infected", result.Error);

        // The three things that matter after a positive: no row, no object, and an audit trail.
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.ChatAttachments.AnyAsync(a => a.StorageKey == key));
        Assert.False(await scope.ServiceProvider.GetRequiredService<IStorage>().ExistsAsync(key));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "chat.attachment_rejected"));
    }

    [Fact]
    public async Task An_infected_upload_cannot_be_confirmed_by_retrying()
    {
        using var scope = _factory.Services.CreateScope();
        var (first, key) = await UploadAsync(scope, Eicar(), "text/plain", "again.txt");
        Assert.Equal("file_infected", first.Error);

        // The object was deleted, so a retry cannot find bytes to confirm — a rejection is not a state a
        // caller can grind past by repeating the call.
        var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
        var retry = await chat.ConfirmAttachmentAsync(RoomId, MemberId, key);
        Assert.False(retry.Ok);
        Assert.Equal("upload_not_found", retry.Error);
    }

    [Fact]
    public async Task Real_media_survives_a_real_scan()
    {
        using var scope = _factory.Services.CreateScope();
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var (result, _) = await UploadAsync(scope, png, "image/png", "photo.png");

        // Proves the scanner is in the path without being in the way: dimensions still come from decoding
        // the bytes, so format inspection and scanning both ran.
        Assert.True(result.Ok, result.Error);
        Assert.Equal(1, result.Value!.Width);
        Assert.Equal(1, result.Value.Height);
    }
}

/// <summary>D-298 fail-closed verification: the scanner is configured, the daemon is not there.</summary>
public class ClamAvUnavailableUploadTests : ClamAvUploadTestBase, IClassFixture<UnreachableClamAvFactory>
{
    private readonly UnreachableClamAvFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;
    private static Guid _roomId, _memberId;

    public ClamAvUnavailableUploadTests(UnreachableClamAvFactory factory)
    {
        _factory = factory;
        lock (ResetLock)
        {
            if (!_reset) { SeedRoom(factory, "98100450", "clamav-down"); _roomId = RoomId; _memberId = MemberId; _reset = true; }
        }
        RoomId = _roomId; MemberId = _memberId;
    }

    [Fact]
    public async Task A_clean_file_is_refused_when_the_scanner_cannot_be_reached()
    {
        using var scope = _factory.Services.CreateScope();
        var (result, key) = await UploadAsync(scope, CleanText(), "text/plain", "notes.txt");

        // FAIL CLOSED. The file is harmless and it is still refused, because "we could not check" and
        // "it is fine" are different answers and only one of them may reach a room.
        Assert.False(result.Ok);
        Assert.Equal("scan_unavailable", result.Error);

        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        Assert.False(await db.ChatAttachments.AnyAsync(a => a.StorageKey == key));
        Assert.False(await scope.ServiceProvider.GetRequiredService<IStorage>().ExistsAsync(key));
    }
}
