using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Kurx.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        // Meta verification handshake (GET). Fail closed in production: the verify token MUST be configured — there is
        // no dev-default fallback outside Development, so a misconfigured prod deployment can't complete the handshake.
        app.MapGet("/v1/webhooks/whatsapp", (
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromQuery(Name = "hub.challenge")] string? challenge,
            IConfiguration config, IHostEnvironment env, ILoggerFactory lf) =>
        {
            var expected = config["WHATSAPP_VERIFY_TOKEN"];
            if (string.IsNullOrEmpty(expected))
            {
                if (env.IsProduction())
                {
                    lf.CreateLogger("WhatsAppWebhook").LogError(
                        "WHATSAPP_VERIFY_TOKEN is not configured — rejecting the verification handshake (fail-closed).");
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
                expected = "kurx-dev-verify-token";   // Development-only convenience
            }
            if (mode == "subscribe" && verifyToken == expected && challenge is not null)
                return Results.Ok(challenge);
            return Results.Forbid();
        }).WithTags("webhooks").AllowAnonymous().Produces<string>();

        // Delivery-status callbacks (POST). Signature is HMAC-SHA256 (Meta). Fail closed in production when the secret
        // is missing; a transient processing failure returns a retryable 500 (never a silent swallow) — safe because
        // HandleWebhookAsync is idempotent (upsert keyed by wamid) and Meta caps its own retries.
        app.MapPost("/v1/webhooks/whatsapp", async (HttpRequest req, IWhatsAppLogService svc, IConfiguration config,
            IHostEnvironment env, ILoggerFactory lf, CancellationToken ct) =>
        {
            var log = lf.CreateLogger("WhatsAppWebhook");
            string body;
            using (var reader = new StreamReader(req.Body)) body = await reader.ReadToEndAsync(ct);

            var appSecret = config["WHATSAPP_APP_SECRET"];
            if (string.IsNullOrEmpty(appSecret))
            {
                if (env.IsProduction())
                {
                    log.LogError("WhatsApp webhook received but WHATSAPP_APP_SECRET is not configured — rejecting (fail-closed).");
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
                log.LogWarning("WhatsApp webhook signature NOT verified (WHATSAPP_APP_SECRET unset) — Development only.");
            }
            else
            {
                var sig = req.Headers["X-Hub-Signature-256"].FirstOrDefault();
                if (!VerifyHmacSignature(body, appSecret, sig))
                {
                    log.LogWarning("WhatsApp webhook rejected: invalid HMAC signature.");
                    return Results.Forbid();
                }
            }

            try
            {
                var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (!root.TryGetProperty("entry", out var entries)) return Results.Ok();
                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes)) continue;
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var val)) continue;
                        if (!val.TryGetProperty("statuses", out var statuses)) continue;
                        foreach (var s in statuses.EnumerateArray())
                        {
                            var wamid = s.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                            var status = s.TryGetProperty("status", out var stProp) ? stProp.GetString() : null;
                            if (wamid is not null && status is not null)
                                await svc.HandleWebhookAsync(wamid, status, body, ct);   // idempotent upsert by wamid
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Never swallow: log and return a retryable status so the provider retries. The handler is idempotent,
                // so a redelivery re-applies the same status without duplicate processing.
                log.LogError(ex, "WhatsApp webhook processing failed — returning 500 for provider retry.");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Results.Ok();
            // Deliberately an empty 200, never 204: Meta treats any 2xx as delivered, and the ack has no
            // body to carry. Declared explicitly so the contract states "no body" rather than leaving it
            // undeclared, which is indistinguishable from an unannotated endpoint.
        }).WithTags("webhooks").AllowAnonymous().Produces(StatusCodes.Status200OK);

        // Razorpay payment webhook (M10, D-049). Signature verification is fail-closed via the gateway (the production
        // RazorpayPaymentGateway enforces its HMAC secret; the dev mock is Development-only). A transient failure
        // returns a retryable 500 (never a silent swallow) — safe because ConfirmPaymentAsync is idempotent (§17.1
        // atomic Pending→Paid claim), so a provider retry re-confirms rather than issuing a second ticket.
        app.MapPost("/v1/webhooks/razorpay", async (HttpRequest req, IOrderService orders, IPaymentGateway gateway,
            ILoggerFactory lf, CancellationToken ct) =>
        {
            var log = lf.CreateLogger("RazorpayWebhook");
            string body;
            using (var reader = new StreamReader(req.Body)) body = await reader.ReadToEndAsync(ct);
            var signature = req.Headers["X-Razorpay-Signature"].FirstOrDefault() ?? "";
            if (!gateway.VerifyWebhookSignature(body, signature))
            {
                log.LogWarning("Razorpay webhook rejected: invalid signature.");
                return Results.Forbid();
            }

            try
            {
                var root = JsonDocument.Parse(body).RootElement;
                var orderId = root.TryGetProperty("order_id", out var o) ? o.GetString() : null;
                var paymentId = root.TryGetProperty("payment_id", out var p) ? p.GetString() : null;
                if (!string.IsNullOrEmpty(orderId) && !string.IsNullOrEmpty(paymentId))
                    await orders.ConfirmPaymentAsync(orderId, paymentId, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Razorpay webhook confirm failed — returning 500 for provider retry.");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Results.Ok();
            // Empty 200 for the same reason as the WhatsApp ack above — Razorpay only checks the status.
        }).WithTags("webhooks").AllowAnonymous().Produces(StatusCodes.Status200OK);
    }

    // Verifies the X-Hub-Signature-256 header using HMAC-SHA256 with the app secret.
    // Uses fixed-time comparison to prevent timing attacks.
    private static bool VerifyHmacSignature(string body, string secret, string? signature)
    {
        if (signature is null || !signature.StartsWith("sha256=", StringComparison.Ordinal))
            return false;

        var expected = Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(body)))
            .ToLowerInvariant();

        var provided = signature[7..]; // strip "sha256=" prefix
        if (expected.Length != provided.Length) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(provided),
            Encoding.ASCII.GetBytes(expected));
    }
}
