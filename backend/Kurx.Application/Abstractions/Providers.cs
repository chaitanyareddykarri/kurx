namespace Kurx.Application.Abstractions;

// Every external service sits behind one of these interfaces.
// Provider selection is env-driven (see .env.example); dev default = console/mock/localdisk.

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailSender
{
    /// <returns>Provider message id, or null when the provider doesn't supply one.</returns>
    Task<string?> SendAsync(string to, string subject, string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken ct = default);
}

public interface IWhatsAppSender
{
    Task SendTextAsync(string phone, string text, CancellationToken ct = default);
    /// <summary>Sends a media message (e.g. ticket QR PNG) with a caption. mediaUrl must be fetchable by the channel.</summary>
    Task SendMediaAsync(string phone, string mediaUrl, string caption, CancellationToken ct = default);
}

public record GatewayOrder(string GatewayOrderId, long AmountPaise, string Currency);
public record GatewayRefundResult(string GatewayRefundId, string Status);

public interface IPaymentGateway
{
    Task<GatewayOrder> CreateOrderAsync(Guid orderId, long amountPaise, CancellationToken ct = default);
    Task<GatewayRefundResult> RefundAsync(string gatewayPaymentId, long amountPaise, CancellationToken ct = default);
    /// <summary>Verifies a webhook signature (HMAC-SHA256 of raw body).</summary>
    bool VerifyWebhookSignature(string rawBody, string signature);
}

public record LinkedAccount(string LinkedAccountId);
public record RouteTransferResult(string TransferId, string Status);

public interface IRouteClient
{
    Task<LinkedAccount> CreateLinkedAccountAsync(Guid orgId, string legalName, string accountNumber, string ifsc, CancellationToken ct = default);
    Task<RouteTransferResult> CreateOnHoldTransferAsync(string gatewayPaymentId, string linkedAccountId, long amountPaise, DateTime? holdUntil, CancellationToken ct = default);
    Task<RouteTransferResult> ReleaseTransferAsync(string transferId, CancellationToken ct = default);
    Task<RouteTransferResult> ReverseTransferAsync(string transferId, long amountPaise, CancellationToken ct = default);
}

public record PresignedUpload(string Key, string Url, IReadOnlyDictionary<string, string> Headers);

/// <summary>Outcome of scanning a stored object for malware (D-110).</summary>
public enum FileScanResult
{
    /// <summary>No threat found. The only value that lets an attachment become visible.</summary>
    Clean,
    /// <summary>A threat was found. The object must never be served and must be removed or quarantined.</summary>
    Infected,
    /// <summary>The scanner could not complete — unreachable, timed out, errored. NOT a clean result.</summary>
    ScanFailed,
    /// <summary>The scanner does not handle this file type. A policy decision, not a verdict.</summary>
    Unsupported,
}

/// <summary>
/// Malware scanning for uploaded objects. Provider-agnostic by design so ClamAV, a cloud scanning
/// service, or anything else slots in without the chat layer changing.
///
/// The pipeline ALWAYS calls this, even when the configured implementation is a no-op — that is what
/// keeps the call site stable when a real scanner is wired, so enabling protection becomes a
/// configuration change rather than a code change.
/// </summary>
public interface IFileScanner
{
    /// <summary>Scans an object already written to storage. Implementations must not throw: an
    /// internal failure is reported as <see cref="FileScanResult.ScanFailed"/>, because a scanner
    /// that throws would otherwise be indistinguishable from a clean file.</summary>
    Task<FileScanResult> ScanAsync(string storageKey, CancellationToken ct = default);
}

public interface IStorage
{
    Task<PresignedUpload> PresignPutAsync(string key, string contentType, long maxBytes, CancellationToken ct = default);
    Task<string> PresignGetAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default);
    Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default);
    Task<byte[]> GetAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public record KycResult(bool Approved, string? Detail);

public interface IKycProvider
{
    /// <summary>Penny-drop bank account verification; returns registered holder name on success.</summary>
    Task<KycResult> PennyDropAsync(string accountNumber, string ifsc, string holderName, CancellationToken ct = default);
    Task<KycResult> PanMatchAsync(string pan, string name, CancellationToken ct = default);
    Task<KycResult> DigilockerAsync(string payloadJson, CancellationToken ct = default);
}

public interface IPushSender
{
    Task SendAsync(string fcmToken, string title, string body, IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default);
}

public record SmsSendResult(bool Ok, string? ProviderMessageId = null, string? Error = null);

/// <summary>Per-send SMS options. India DLT compliance requires EntityId + TemplateId on the SNS
/// provider; SenderId is the registered alphanumeric/numeric sender id.</summary>
public record SmsSendOptions(string? SenderId = null, string? EntityId = null, string? TemplateId = null);

/// <summary>Transactional SMS delivery (AM1, ADR-A5). Primary impl is AWS SNS; this seam lets
/// Twilio Verify / Vonage / MessageBird slot in later without touching business logic.</summary>
public interface ISmsProvider
{
    /// <param name="e164Phone">Destination in E.164 (e.g. +14155552671).</param>
    Task<SmsSendResult> SendAsync(string e164Phone, string message, SmsSendOptions? options = null, CancellationToken ct = default);
    string Name { get; }
}

public record CertificateRenderRequest(
    string TemplateLayoutKey,
    string RecipientName,
    string EventName,
    string EventDate,
    string? SignatoryName,
    string? SignatoryTitle,
    string? LogoKey,
    string? BackgroundKey,
    string? AccentColor,
    string QrDataUrl);

public record CertificateRenderResult(byte[] PdfBytes, byte[] PngBytes);

public interface ICertificateRenderer
{
    /// <summary>Renders a certificate using the given template layout and merge fields.
    /// Returns both a PDF (A4) and a PNG preview (1240×877 @ 150 dpi).</summary>
    Task<CertificateRenderResult> RenderAsync(CertificateRenderRequest request, CancellationToken ct = default);
}

public interface IQrCodeGenerator
{
    /// <summary>Generates a square QR code PNG for the given data string.
    /// <paramref name="pixelSize"/> controls the output image dimension (default 512).</summary>
    byte[] GeneratePng(string data, int pixelSize = 512);
}

public interface IDocumentRasterizer
{
    /// <summary>Converts a single PDF page to a PNG at the requested DPI.
    /// Used to produce certificate preview thumbnails from QuestPDF output.</summary>
    byte[] RasterizePage(byte[] pdfBytes, int pageIndex = 0, int dpi = 150);
}
