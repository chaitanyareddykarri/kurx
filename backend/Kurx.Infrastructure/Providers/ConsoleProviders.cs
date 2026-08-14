using System.Text.RegularExpressions;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Providers;

// Dev providers: log what a real provider would send. Zero credentials, zero network.

public partial class ConsoleEmailSender(ILogger<ConsoleEmailSender> log) : IEmailSender
{
    /// <summary>Longest body printed. A one-time code is one short sentence; a certificate mail is a
    /// page of markup, and flooding the log is how a useful line becomes an ignored one.</summary>
    private const int MaxBodyChars = 400;

    public Task<string?> SendAsync(string to, string subject, string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken ct = default)
    {
        // The body is logged for the same reason ConsoleSmsProvider logs its message: this is the
        // zero-credential dev sender, nothing leaves the process, and a code nobody can read is a
        // flow nobody can finish. Only the subject was printed before, so every email-addressed
        // ceremony — verify your address, and therefore email as a second factor at sign-in — was
        // impossible to complete locally: the code is HMAC-peppered in `otp_codes` and unrecoverable
        // from the database too. SMS was readable and email was not, which is why one worked in dev
        // and the other silently did not.
        //
        // Production is unaffected: EMAIL_PROVIDER=ses selects SesEmailSender, which logs no body.
        log.LogInformation("[email→console] to={To} subject={Subject} attachments={Count} body={Body}",
            to, subject, attachments?.Count ?? 0, PlainText(htmlBody));
        return Task.FromResult<string?>($"console-{Guid.NewGuid():N}");
    }

    /// <summary>Tags stripped and whitespace collapsed, so <c>&lt;p&gt;Your Kurx code is 123456.&lt;/p&gt;</c>
    /// reads as one line — the same shape the SMS sender prints.</summary>
    private static string PlainText(string html)
    {
        var text = WhitespaceRun().Replace(TagRun().Replace(html ?? "", " "), " ").Trim();
        return text.Length <= MaxBodyChars ? text : text[..MaxBodyChars] + "…";
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRun();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}

public class ConsoleWhatsAppSender(ILogger<ConsoleWhatsAppSender> log) : IWhatsAppSender
{
    public Task SendTextAsync(string phone, string text, CancellationToken ct = default)
    {
        log.LogInformation("[whatsapp→console] to={Phone} text={Text}", phone, text);
        return Task.CompletedTask;
    }

    public Task SendMediaAsync(string phone, string mediaUrl, string caption, CancellationToken ct = default)
    {
        log.LogInformation("[whatsapp→console] to={Phone} media={Url} caption={Caption}", phone, mediaUrl, caption);
        return Task.CompletedTask;
    }
}

public class ConsolePushSender(ILogger<ConsolePushSender> log) : IPushSender
{
    public Task SendAsync(string fcmToken, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        log.LogInformation("[push→console] token={Token} title={Title} body={Body}", fcmToken, title, body);
        return Task.CompletedTask;
    }
}
