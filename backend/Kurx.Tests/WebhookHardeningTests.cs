using System.Net;
using System.Security.Cryptography;
using System.Text;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>Backend hardening sprint — webhook security + provider configuration. Verifies signature verification,
/// replay/duplicate idempotency, that a transient processing failure returns a retryable status (never a silent
/// swallow), and that the provider boundary fails fast on an unimplemented provider. Real HTTP / real kurx_test.</summary>
public class WebhookHardeningTests : IClassFixture<KurxApiFactory>
{
    private readonly KurxApiFactory _factory;
    public WebhookHardeningTests(KurxApiFactory factory) => _factory = factory;

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
    private static string Hmac(string secret, string body) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    // ── no silent swallow: a processing exception returns a retryable 500 ────────────
    [Fact]
    public async Task Whatsapp_webhook_malformed_body_returns_retryable_500_not_swallowed()
    {
        // Dev: signature is skipped (no secret), so the malformed body reaches JsonDocument.Parse → throws → caught.
        var res = await _factory.CreateClient().PostAsync("/v1/webhooks/whatsapp", Json("{not valid json"));
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);   // retryable — the old code returned 200 (swallowed)
    }

    // ── signature verification (valid / invalid) ─────────────────────────────────────
    [Fact]
    public async Task Whatsapp_webhook_invalid_signature_is_rejected()
    {
        using var f = _factory.WithWebHostBuilder(b => b.UseSetting("WHATSAPP_APP_SECRET", "webhook-secret"));
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/whatsapp") { Content = Json("{\"entry\":[]}") };
        req.Headers.Add("X-Hub-Signature-256", "sha256=0000000000000000000000000000000000000000000000000000000000000000");
        var res = await f.CreateClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Whatsapp_webhook_valid_signature_is_accepted()
    {
        const string secret = "webhook-secret";
        const string body = "{\"entry\":[]}";
        using var f = _factory.WithWebHostBuilder(b => b.UseSetting("WHATSAPP_APP_SECRET", secret));
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/whatsapp") { Content = Json(body) };
        req.Headers.Add("X-Hub-Signature-256", Hmac(secret, body));
        var res = await f.CreateClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Whatsapp_webhook_missing_signature_is_rejected_when_secret_configured()
    {
        using var f = _factory.WithWebHostBuilder(b => b.UseSetting("WHATSAPP_APP_SECRET", "webhook-secret"));
        var res = await f.CreateClient().PostAsync("/v1/webhooks/whatsapp", Json("{\"entry\":[]}"));   // no signature header
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ── replay / duplicate delivery is idempotent ───────────────────────────────────
    [Fact]
    public async Task Whatsapp_webhook_duplicate_delivery_is_idempotent()
    {
        var wamid = "wamid." + Guid.NewGuid().ToString("N");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            db.WhatsAppMessages.Add(new WhatsAppMessage
            {
                ToPhone = "919000000000", Template = "otp", Kind = WhatsAppMessageKind.Generic,
                Wamid = wamid, Status = WhatsAppMessageStatus.Sent, RelatedType = "event",
            });
            await db.SaveChangesAsync();
        }

        var body = $"{{\"entry\":[{{\"changes\":[{{\"value\":{{\"statuses\":[{{\"id\":\"{wamid}\",\"status\":\"delivered\"}}]}}}}]}}]}}";
        var client = _factory.CreateClient();
        var first = await client.PostAsync("/v1/webhooks/whatsapp", Json(body));
        var replay = await client.PostAsync("/v1/webhooks/whatsapp", Json(body));   // duplicate delivery
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
            var msg = await db.WhatsAppMessages.FirstAsync(m => m.Wamid == wamid);
            Assert.Equal(WhatsAppMessageStatus.Delivered, msg.Status);   // applied once; the replay didn't corrupt or error
        }
    }

    [Fact]
    public async Task Whatsapp_webhook_unknown_wamid_is_handled_gracefully()
    {
        var body = $"{{\"entry\":[{{\"changes\":[{{\"value\":{{\"statuses\":[{{\"id\":\"wamid.{Guid.NewGuid():N}\",\"status\":\"read\"}}]}}}}]}}]}}";
        var res = await _factory.CreateClient().PostAsync("/v1/webhooks/whatsapp", Json(body));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);   // no matching message → no-op, not an error
    }

    // ── provider boundary fails fast on an unimplemented provider ─────────────────────
    [Fact]
    public void Provider_config_rejects_an_unimplemented_provider()
    {
        // Boot the real host with an unimplemented provider flag — startup must throw (fail-fast), not silently
        // fall back to the dev implementation. Uses the reachable test DB so only the provider selector fails.
        using var f = _factory.WithWebHostBuilder(b => b.UseSetting("EMAIL_PROVIDER", "sendgrid"));
        var ex = Record.Exception(() => f.CreateClient());
        Assert.NotNull(ex);
        Assert.Contains("EMAIL_PROVIDER", ex!.Message + (ex.InnerException?.Message ?? ""));   // NotSupportedException surfaced
    }
}
