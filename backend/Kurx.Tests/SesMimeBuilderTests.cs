using System.Text;
using Kurx.Application.Abstractions;
using Kurx.Infrastructure.Providers;
using Xunit;

namespace Kurx.Tests;

/// <summary>
/// The hand-rolled MIME builder behind <see cref="SesEmailSender"/> (D-284).
/// </summary>
/// <remarks>
/// <para>Written by hand rather than pulled from a library because this is the only place in the platform
/// that needs MIME and the shape required is small and fixed. That trade is only defensible with tests:
/// malformed MIME does not throw, it arrives — as a mail client showing a certificate as gibberish, or a
/// receiver rejecting an over-long base64 line outright. No integration test would catch either, because
/// nothing on our side fails.</para>
///
/// <para>These are pure assertions over the produced bytes. No SES client, no network, no database.</para>
/// </remarks>
public class SesMimeBuilderTests
{
    private const string From = "Kurx <no-reply@kurx.in>";
    private const string To = "person@example.com";

    private static string Build(string subject, string html, params EmailAttachment[] attachments)
    {
        using var stream = SesEmailSender.BuildMimeMessage(From, To, subject, html, attachments);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static EmailAttachment Pdf(string name, int bytes) =>
        new(name, "application/pdf", Enumerable.Range(0, bytes).Select(i => (byte)(i % 256)).ToArray());

    [Fact]
    public void The_envelope_carries_the_sender_recipient_and_a_multipart_declaration()
    {
        var mime = Build("Your ticket", "<p>Hi</p>", Pdf("ticket.pdf", 32));

        Assert.Contains($"From: {From}\r\n", mime);
        Assert.Contains($"To: {To}\r\n", mime);
        Assert.Contains("MIME-Version: 1.0\r\n", mime);
        Assert.Contains("Content-Type: multipart/mixed; boundary=\"", mime);
    }

    /// <summary>Every part must open with the declared boundary and the message must close with the
    /// terminating one. A missing final <c>--</c> is the classic hand-built-MIME bug: most clients render it
    /// anyway, and strict receivers reject the whole message.</summary>
    [Fact]
    public void The_boundary_opens_every_part_and_closes_the_message()
    {
        var mime = Build("Subject", "<p>Body</p>", Pdf("a.pdf", 16), Pdf("b.pdf", 16));

        var declared = mime.Split("boundary=\"")[1].Split('"')[0];
        // One body part + two attachments = three opening delimiters.
        Assert.Equal(3, CountOccurrences(mime, $"--{declared}\r\n"));
        Assert.EndsWith($"--{declared}--\r\n", mime);
    }

    /// <summary>RFC 2045 caps an <b>encoded content</b> line at 76 characters; longer lines are rejected
    /// outright by some receivers, so this is the assertion that most directly protects delivery.
    ///
    /// <para>Scoped to the payload bodies deliberately. The limit does not apply to headers — the
    /// <c>Content-Type: multipart/mixed; boundary="…"</c> line is 84 characters and is entirely legal, since
    /// headers fold under RFC 5322's separate and far longer rule. An assertion over every line would fail
    /// on correct output, which is a worse test than none.</para></summary>
    [Fact]
    public void Base64_payloads_never_exceed_the_line_length_limit()
    {
        // Large enough to force many wraps in both the body and the attachment.
        var mime = Build("Subject", new string('x', 5000), Pdf("big.pdf", 4096));
        var declared = mime.Split("boundary=\"")[1].Split('"')[0];

        var parts = mime.Split($"--{declared}").Skip(1).ToList();
        Assert.True(parts.Count >= 2, "precondition: this fixture must produce a body part and an attachment");

        foreach (var part in parts)
        {
            var split = part.Split("\r\n\r\n", 2);
            if (split.Length < 2) continue;                       // the closing "--" delimiter has no body
            var payload = split[1].Replace($"--{declared}--", "");

            foreach (var line in payload.Split("\r\n").Where(l => l.Length > 0))
                Assert.True(line.Length <= 76,
                    $"a {line.Length}-character encoded line exceeds the 76-char limit in RFC 2045");
        }
    }

    /// <summary>A short payload must not be wrapped at all — wrapping introduces a CRLF that a decoder has
    /// to strip, and getting the boundary condition wrong is how off-by-one bugs enter.</summary>
    [Fact]
    public void A_short_payload_is_emitted_on_a_single_line()
    {
        var mime = Build("Subject", "<p>hi</p>");

        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes("<p>hi</p>"));
        Assert.True(body.Length <= 76, "precondition: this fixture must be short enough not to wrap");
        Assert.Contains(body, mime);
    }

    /// <summary>The body must survive the round trip exactly. Base64 is only useful if it decodes back.</summary>
    [Fact]
    public void The_html_body_round_trips_through_base64()
    {
        const string html = "<p>Your certificate is attached — congratulations!</p>";
        var mime = Build("Subject", html);

        Assert.Contains("Content-Type: text/html; charset=UTF-8\r\n", mime);
        Assert.Contains("Content-Transfer-Encoding: base64\r\n", mime);
        Assert.True(DecodesTo(mime, html), "the HTML body did not survive encoding");
    }

    /// <summary>A non-ASCII subject must be encoded-word wrapped, not emitted raw. A raw UTF-8 subject header
    /// is mangled by receivers that assume ASCII — and Indian names and event titles routinely contain
    /// non-ASCII characters, so this is the common case, not the exotic one.</summary>
    [Fact]
    public void A_non_ascii_subject_is_encoded_rather_than_emitted_raw()
    {
        const string subject = "आपका टिकट — Kurx";
        var mime = Build(subject, "<p>x</p>");

        Assert.DoesNotContain(subject, mime);
        Assert.Contains("Subject: =?UTF-8?B?", mime);

        var encoded = mime.Split("Subject: =?UTF-8?B?")[1].Split("?=")[0];
        Assert.Equal(subject, Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
    }

    /// <summary>Each attachment needs its own content type and a filename the client will show. A missing
    /// <c>Content-Disposition</c> makes an attachment render inline as binary noise.</summary>
    [Fact]
    public void Each_attachment_declares_its_type_and_filename()
    {
        var mime = Build("Subject", "<p>x</p>", Pdf("certificate.pdf", 64), Pdf("ticket.pdf", 64));

        Assert.Contains("Content-Type: application/pdf\r\n", mime);
        Assert.Contains("Content-Disposition: attachment; filename=\"certificate.pdf\"\r\n", mime);
        Assert.Contains("Content-Disposition: attachment; filename=\"ticket.pdf\"\r\n", mime);
    }

    /// <summary>Attachment bytes must survive exactly — a certificate PDF that decodes to anything else is
    /// a corrupt file the recipient cannot open.</summary>
    [Fact]
    public void Attachment_bytes_round_trip_exactly()
    {
        var payload = Enumerable.Range(0, 512).Select(i => (byte)(i % 256)).ToArray();
        var mime = Build("Subject", "<p>x</p>", new EmailAttachment("doc.pdf", "application/pdf", payload));

        // The last part's payload is everything between the final header break and the closing boundary.
        var declared = mime.Split("boundary=\"")[1].Split('"')[0];
        var part = mime.Split($"--{declared}\r\n")[^1];
        var encoded = part.Split("\r\n\r\n")[1].Split($"--{declared}--")[0].Replace("\r\n", "").TrimEnd();

        Assert.Equal(payload, Convert.FromBase64String(encoded));
    }

    /// <summary>Zero attachments still produces a well-formed single-part message. The caller never uses this
    /// path today (it takes the Simple content branch), but the builder must not emit a dangling boundary if
    /// it ever does.</summary>
    [Fact]
    public void A_message_with_no_attachments_is_still_well_formed()
    {
        var mime = Build("Subject", "<p>only a body</p>");

        var declared = mime.Split("boundary=\"")[1].Split('"')[0];
        Assert.Equal(1, CountOccurrences(mime, $"--{declared}\r\n"));
        Assert.EndsWith($"--{declared}--\r\n", mime);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static bool DecodesTo(string mime, string expected)
    {
        var target = Convert.ToBase64String(Encoding.UTF8.GetBytes(expected));
        return mime.Replace("\r\n", "").Contains(target.Replace("\r\n", ""), StringComparison.Ordinal);
    }
}
