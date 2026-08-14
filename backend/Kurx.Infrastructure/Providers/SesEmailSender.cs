using System.Text;
using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Providers;

/// <summary>AWS SES v2 transactional email (D-284). Credentials come from the default AWS chain (env
/// AWS_ACCESS_KEY_ID/SECRET, or a task role) — never hard-coded. Dormant until EMAIL_PROVIDER=ses.
///
/// <para>The provider message id SES returns is propagated to the caller rather than discarded, so a send can
/// be correlated to a delivery, bounce or complaint event later.</para></summary>
public class SesEmailSender : IEmailSender, IDisposable
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly ILogger<SesEmailSender> _log;
    private readonly string _from;
    private readonly string? _configurationSet;

    public SesEmailSender(IConfiguration config, ILogger<SesEmailSender> log)
    {
        _log = log;
        var region = config["AWS_REGION"] ?? "ap-south-1";
        _ses = new AmazonSimpleEmailServiceV2Client(RegionEndpoint.GetBySystemName(region));

        // A verified identity is required by SES; there is no sensible default, and sending from an
        // unverified address fails at the API with an error that reads like a permissions problem.
        var address = config["SES_FROM_ADDRESS"]
            ?? throw new InvalidOperationException(
                "SES_FROM_ADDRESS is not set. SES refuses to send from an unverified identity, so this "
                + "cannot be defaulted. Set it to the verified sending address.");
        var name = config["SES_FROM_NAME"];
        _from = string.IsNullOrWhiteSpace(name) ? address : $"{name} <{address}>";

        // Optional: the configuration set is what routes bounce/complaint events to a destination. Absent
        // today because that pipeline is deployment work (D-284), present as config so enabling it later
        // needs no code change.
        _configurationSet = config["SES_CONFIGURATION_SET"];
    }

    public async Task<string?> SendAsync(string to, string subject, string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken ct = default)
    {
        try
        {
            var request = new SendEmailRequest
            {
                FromEmailAddress = _from,
                Destination = new Destination { ToAddresses = [to] },
                Content = attachments is { Count: > 0 }
                    // Attachments require raw MIME: SES's Simple content has no attachment field at all.
                    ? new EmailContent { Raw = new RawMessage { Data = BuildMimeMessage(_from, to, subject, htmlBody, attachments) } }
                    : new EmailContent
                    {
                        Simple = new Message
                        {
                            Subject = new Content { Data = subject, Charset = "UTF-8" },
                            Body = new Body { Html = new Content { Data = htmlBody, Charset = "UTF-8" } },
                        },
                    },
            };
            if (!string.IsNullOrWhiteSpace(_configurationSet))
                request.ConfigurationSetName = _configurationSet;

            var response = await _ses.SendEmailAsync(request, ct);
            return response.MessageId;
        }
        catch (Exception ex)
        {
            // Never leak provider internals to the caller; callers treat a null id / thrown send as a soft
            // failure. Logged at Error because an email that silently never arrives is invisible otherwise.
            _log.LogError(ex, "SES send failed to {Recipient}", to);
            throw;
        }
    }

    /// <summary>
    /// Builds a minimal multipart/mixed MIME message: an HTML body part plus one part per attachment.
    /// </summary>
    /// <remarks>
    /// Hand-built rather than pulling in a MIME library, because this is the only place in the platform that
    /// needs one and the shape required here is small and fixed. If a second caller ever needs richer MIME
    /// (inline images, alternative text parts, calendar invites), replace this with a real library rather
    /// than growing it — that is the point at which hand-rolling stops being the cheaper option.
    ///
    /// <para>Base64 payloads are wrapped at 76 characters because RFC 2045 caps an encoded line at 76, and
    /// some receivers reject longer ones outright rather than folding them.</para>
    /// </remarks>
    /// <remarks><b>Static and internal</b> so it can be exercised directly. The rest of this class needs a
    /// live SES client and a verified sending identity to construct at all, which would make the only
    /// non-trivial logic here untestable — and hand-built MIME fails in ways that are invisible until a
    /// recipient's client renders the message wrongly.</remarks>
    internal static MemoryStream BuildMimeMessage(string from, string to, string subject, string htmlBody,
        IReadOnlyList<EmailAttachment> attachments)
    {
        var boundary = $"--boundary_{Guid.NewGuid():N}";
        var mime = new StringBuilder();

        mime.Append("From: ").Append(from).Append("\r\n");
        mime.Append("To: ").Append(to).Append("\r\n");
        // Encoded-word so a non-ASCII subject survives; base64 is safe for any content.
        mime.Append("Subject: =?UTF-8?B?")
            .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))).Append("?=\r\n");
        mime.Append("MIME-Version: 1.0\r\n");
        mime.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n\r\n");

        mime.Append("--").Append(boundary).Append("\r\n");
        mime.Append("Content-Type: text/html; charset=UTF-8\r\n");
        mime.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        mime.Append(Wrap(Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlBody)))).Append("\r\n");

        foreach (var attachment in attachments)
        {
            mime.Append("--").Append(boundary).Append("\r\n");
            mime.Append("Content-Type: ").Append(attachment.ContentType).Append("\r\n");
            mime.Append("Content-Transfer-Encoding: base64\r\n");
            mime.Append("Content-Disposition: attachment; filename=\"").Append(attachment.FileName).Append("\"\r\n\r\n");
            mime.Append(Wrap(Convert.ToBase64String(attachment.Content))).Append("\r\n");
        }

        mime.Append("--").Append(boundary).Append("--\r\n");
        return new MemoryStream(Encoding.UTF8.GetBytes(mime.ToString()));
    }

    private static string Wrap(string base64)
    {
        const int lineLength = 76;
        if (base64.Length <= lineLength) return base64;

        var wrapped = new StringBuilder(base64.Length + base64.Length / lineLength * 2);
        for (var offset = 0; offset < base64.Length; offset += lineLength)
        {
            if (offset > 0) wrapped.Append("\r\n");
            wrapped.Append(base64, offset, Math.Min(lineLength, base64.Length - offset));
        }
        return wrapped.ToString();
    }

    public void Dispose() => _ses.Dispose();
}
