using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Kurx.Infrastructure.Auth;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Providers;

/// <summary>Dev SMS provider: logs what a real provider would send. Zero credentials, zero network.</summary>
public class ConsoleSmsProvider(ILogger<ConsoleSmsProvider> log) : ISmsProvider
{
    public string Name => "console";

    public Task<SmsSendResult> SendAsync(string e164Phone, string message,
        SmsSendOptions? options = null, CancellationToken ct = default)
    {
        log.LogInformation("[sms→console] to={Phone} senderId={SenderId} template={Template} msg={Message}",
            e164Phone, options?.SenderId, options?.TemplateId, message);
        return Task.FromResult(new SmsSendResult(true, $"console-{Guid.NewGuid():N}"));
    }
}

/// <summary>AWS SNS transactional SMS (AM1, ADR-A5/AM21). India DLT compliance is carried via the
/// <c>AWS.MM.SMS.EntityId</c> / <c>AWS.MM.SMS.TemplateId</c> message attributes; the registered sender
/// id via <c>AWS.SNS.SMS.SenderID</c>. Credentials come from the default AWS chain (env
/// AWS_ACCESS_KEY_ID/SECRET, or an instance role) — never hard-coded. Dormant until SMS_PROVIDER=sns.</summary>
public class SnsSmsProvider : ISmsProvider, IDisposable
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly ILogger<SnsSmsProvider> _log;

    public SnsSmsProvider(IConfiguration config, ILogger<SnsSmsProvider> log)
    {
        _log = log;
        var region = config["AWS_REGION"] ?? "ap-south-1";
        _sns = new AmazonSimpleNotificationServiceClient(RegionEndpoint.GetBySystemName(region));
    }

    public string Name => "sns";

    public async Task<SmsSendResult> SendAsync(string e164Phone, string message,
        SmsSendOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            var attrs = new Dictionary<string, MessageAttributeValue>
            {
                ["AWS.SNS.SMS.SMSType"] = new() { DataType = "String", StringValue = "Transactional" },
            };
            if (!string.IsNullOrWhiteSpace(options?.SenderId))
                attrs["AWS.SNS.SMS.SenderID"] = new() { DataType = "String", StringValue = options.SenderId };
            if (!string.IsNullOrWhiteSpace(options?.EntityId))
                attrs["AWS.MM.SMS.EntityId"] = new() { DataType = "String", StringValue = options.EntityId };
            if (!string.IsNullOrWhiteSpace(options?.TemplateId))
                attrs["AWS.MM.SMS.TemplateId"] = new() { DataType = "String", StringValue = options.TemplateId };

            var resp = await _sns.PublishAsync(
                new PublishRequest { PhoneNumber = e164Phone, Message = message, MessageAttributes = attrs }, ct);
            return new SmsSendResult(true, resp.MessageId);
        }
        catch (Exception ex)
        {
            // Never leak provider internals to the caller; the OTP service treats a failed send as a soft error.
            _log.LogError(ex, "SNS SMS send failed for {Phone}", PhoneCanonicalizer.Mask(e164Phone));
            return new SmsSendResult(false, Error: "sms_send_failed");
        }
    }

    public void Dispose() => _sns.Dispose();
}
