using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// Uploading a participant list over HTTP (D-344, Phase 6).
///
/// <para>The parsing itself is covered exhaustively in <see cref="SpreadsheetReaderTests"/>. What is
/// asserted here is everything the reader cannot see: that the upload is <b>authorized against the
/// event</b> before a byte is parsed, that an unknown event is a 404 rather than a 403 (D-018), that a
/// bad file comes back as a refusal the organiser can act on rather than a 500, and that the response
/// carries the suggested mapping the review screen is built around.</para>
///
/// <para>Nothing is persisted by this endpoint. It reads a file and throws it away — which is the point:
/// the organiser confirms the mapping before anything exists to be wrong.</para>
/// </summary>
public class ParticipantPreviewEndpointTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    private static readonly object ResetLock = new();
    private static bool _reset;

    public ParticipantPreviewEndpointTests(KurxApiFactory factory)
    {
        _factory = factory;
        lock (ResetLock) { if (!_reset) { factory.ResetDatabase(); _reset = true; } }
    }

    private static string Phone() => "9" + Random.Shared.NextInt64(100000000, 999999999);

    /// <summary>Signs a new user in through the real OTP flow and returns their client and id.</summary>
    private async Task<(HttpClient Client, Guid UserId)> SignInAsync()
    {
        var phone = Phone();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/otp/request", new { phone });
        var code = _factory.WhatsApp.LastOtpFor(phone);
        var token = await (await client.PostAsJsonAsync("/v1/auth/otp/verify", new { phone, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
        return (client, token.GetProperty("user_id").GetGuid());
    }

    private async Task<Guid> SeedEventAsync(Guid ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var org = new Organization { Name = "Org " + suffix, Slug = "org" + suffix };
        db.Organizations.Add(org);

        var categoryId = await db.EventCategories.AsNoTracking()
            .Where(c => c.Level == CategoryLevel.Category).Select(c => c.Id).FirstAsync();

        var ev = new Event
        {
            Title = "Hackathon " + suffix,
            Slug = "hackathon-" + suffix,
            ShortCode = $"E{Guid.NewGuid():N}"[..6].ToUpperInvariant(),
            Description = "d", VenueName = "v",
            RepresentingOrgId = org.Id, CreatedBy = ownerId, CategoryId = categoryId,
            StartsAt = DateTime.UtcNow.AddDays(30), EndsAt = DateTime.UtcNow.AddDays(31),
            Status = EventStatus.Published,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev.Id;
    }

    private static MultipartFormDataContent Upload(string content, string fileName) =>
        Upload(Encoding.UTF8.GetBytes(content), fileName);

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", fileName);
        return form;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid eventId, HttpContent body) =>
        client.PostAsync($"/v1/events/{eventId}/certificate-participants/preview", body);

    // ── Authority ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_anonymous_upload_is_refused()
    {
        var (owner, ownerId) = await SignInAsync();
        var eventId = await SeedEventAsync(ownerId);

        var response = await PostAsync(_factory.CreateClient(), eventId,
            Upload("Name\nRahul\n", "p.csv"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        owner.Dispose();
    }

    /// <summary>Authority is checked before the file is read: someone else's event must not be a way to
    /// hand the server a 10MB workbook to parse.</summary>
    [Fact]
    public async Task An_outsider_cannot_upload_to_someone_elses_event()
    {
        var (_, ownerId) = await SignInAsync();
        var eventId = await SeedEventAsync(ownerId);
        var (outsider, _) = await SignInAsync();

        var response = await PostAsync(outsider, eventId, Upload("Name\nRahul\n", "p.csv"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>D-018: an event you may not see is indistinguishable from one that does not exist.</summary>
    [Fact]
    public async Task An_unknown_event_is_a_404()
    {
        var (client, _) = await SignInAsync();

        var response = await PostAsync(client, Guid.NewGuid(), Upload("Name\nRahul\n", "p.csv"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── The read ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_csv_comes_back_with_columns_samples_and_a_suggested_mapping()
    {
        var (client, ownerId) = await SignInAsync();
        var eventId = await SeedEventAsync(ownerId);

        var response = await PostAsync(client, eventId,
            Upload("Name,Team,Award\nRahul Sharma,ByteBuilders,First\nPriya Patel,CodeCrafters,Second\n",
                "participants.csv"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(["Name", "Team", "Award"],
            body.GetProperty("columns").EnumerateArray().Select(c => c.GetString()).ToArray());
        Assert.Equal(2, body.GetProperty("totalRows").GetInt32());
        Assert.False(body.GetProperty("truncated").GetBoolean());

        var mapping = body.GetProperty("suggestedMapping");
        Assert.Equal("participant_name", mapping.GetProperty("Name").GetString());
        Assert.Equal("team_name", mapping.GetProperty("Team").GetString());
        Assert.Equal("achievement", mapping.GetProperty("Award").GetString());

        // Real values, so the organiser can see a column called "Name" that actually holds team names.
        var sample = body.GetProperty("sampleRows")[0];
        Assert.Equal("Rahul Sharma", sample[0].GetString());
    }

    /// <summary>The rule that matters most, asserted end to end: a value survives the whole round trip
    /// as the string the organiser typed.</summary>
    [Fact]
    public async Task Values_are_not_reinterpreted_anywhere_along_the_way()
    {
        var (client, ownerId) = await SignInAsync();
        var eventId = await SeedEventAsync(ownerId);

        var response = await PostAsync(client, eventId,
            Upload("Name,Roll,Phone\nअनन्या राव,007,+919876543210\n", "participants.csv"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = body.GetProperty("sampleRows")[0];

        Assert.Equal("अनन्या राव", row[0].GetString());
        Assert.Equal("007", row[1].GetString());
        Assert.Equal("+919876543210", row[2].GetString());
    }

    /// <summary>Only a handful of rows come back regardless of file size — the mapping screen needs
    /// enough to check a column, not the whole list echoed over the wire.</summary>
    [Fact]
    public async Task Only_a_sample_is_returned_but_the_real_total_is_reported()
    {
        var (client, ownerId) = await SignInAsync();
        var eventId = await SeedEventAsync(ownerId);
        var csv = "Name\n" + string.Concat(Enumerable.Range(0, 40).Select(i => $"Participant {i}\n"));

        var response = await PostAsync(client, eventId, Upload(csv, "participants.csv"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(40, body.GetProperty("totalRows").GetInt32());
        Assert.True(body.GetProperty("sampleRows").GetArrayLength() <= 5);
    }

    // ── Safe failure ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("this is not a spreadsheet", "participants.xlsx", "invalid_spreadsheet")]
    [InlineData("Name,Team\n", "participants.csv", "no_rows")]
    [InlineData("Name\nRahul\n", "participants.numbers", "unsupported_file_type")]
    [InlineData("", "participants.csv", "empty_file")]
    public async Task A_file_that_cannot_be_used_is_refused_with_a_reason(
        string content, string fileName, string expected)
    {
        var (client, ownerId) = await SignInAsync();
        var eventId = await SeedEventAsync(ownerId);

        var response = await PostAsync(client, eventId, Upload(content, fileName));

        // A refusal the organiser can act on — never a 500 that looks like the platform is broken when
        // the upload is what is wrong.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, body.GetProperty("error").GetString());
    }

    /// <summary>An oversized upload is refused rather than read into memory. The declared length is not
    /// trusted on its own — the bytes are counted as they stream.</summary>
    [Fact]
    public async Task An_oversized_upload_is_refused()
    {
        var (client, ownerId) = await SignInAsync();
        var eventId = await SeedEventAsync(ownerId);

        var response = await PostAsync(client, eventId,
            Upload(new byte[11 * 1024 * 1024], "participants.csv"));

        Assert.True(
            response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest,
            $"Expected a refusal, got {(int)response.StatusCode}.");
    }
}
