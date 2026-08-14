using PhoneNumbers;

namespace Kurx.Infrastructure.Auth;

/// <summary>A parsed phone number in the three forms the platform stores (D-089).</summary>
/// <param name="E164">Canonical international form, e.g. <c>+919876543210</c> — the identity column.</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 region, e.g. <c>IN</c>. Derived, never supplied by the client.</param>
/// <param name="National">National significant number, for display in the user's own convention.</param>
public readonly record struct ParsedPhone(string E164, string CountryCode, string National);

/// <summary>E.164 phone canonicalization (ADR-A6, D-089), backed by libphonenumber.
///
/// <para>This replaces the legacy "10 digits ⇒ +91" assumption. That shortcut is not merely
/// India-centric — it is unsafe: a national-format number with no stated region has no honest
/// interpretation, and guessing one routes an OTP to a stranger in another country. So
/// <see cref="TryParse"/> refuses a national number unless the caller states the region.</para>
///
/// <para>The class name deliberately differs from the libphonenumber <c>PhoneNumbers</c> namespace
/// imported above; naming it <c>PhoneNumbers</c> collides with that namespace and makes every
/// reference here ambiguous.</para></summary>
public static class PhoneCanonicalizer
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    /// <summary>Strict-accept canonicalization of an already-international number. Used on the auth
    /// contract surface, where callers are required to submit E.164 already.</summary>
    public static bool TryToE164(string input, out string e164)
    {
        e164 = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!input.Trim().StartsWith('+')) return false;        // international format only
        if (!TryParse(input, region: null, out var parsed)) return false;
        e164 = parsed.E164;
        return true;
    }

    /// <summary>Best-effort E.164, for values that may come from either phone column.
    ///
    /// <para>Callers read whichever column they have: the canonical <c>PhoneE164</c> ("+6591234567") or the
    /// legacy bare-digit <c>Phone</c> ("6591234567"), which is <c>AuthService.NormalizePhone</c> output and
    /// therefore already carries its country code — it is missing only the '+'. Restoring that '+' states
    /// no country the digits did not already state, so this can never re-home a number the way parsing in a
    /// default region would.</para>
    ///
    /// <para>Anything that still fails to validate is returned unchanged, so a bad value fails visibly
    /// downstream rather than silently becoming somebody else's number. Use this wherever two phone values
    /// of possibly-different provenance are compared, so both sides land in the same form.</para></summary>
    public static string ToE164OrUnchanged(string? phone)
    {
        if (TryToE164(phone ?? "", out var e164)) return e164;

        var digits = new string((phone ?? "").Where(char.IsAsciiDigit).ToArray());
        return digits.Length > 0 && TryToE164('+' + digits, out var restored) ? restored : phone ?? "";
    }

    /// <summary>A phone number reduced to what an operator needs and nothing more: the last four digits.
    /// A full number in a log line is PII in a place that is copied, shipped and retained far more freely
    /// than the database is, and the only diagnostic question logs need to answer — "is this the
    /// destination I expected?" — survives masking. Length-agnostic, so it is correct for every numbering
    /// plan rather than assuming a ten-digit national number.</summary>
    public static string Mask(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsAsciiDigit).ToArray());
        return digits.Length <= 4 ? "••••" : "••••" + digits[^4..];
    }

    /// <summary>Parses and validates a number, optionally within a region.</summary>
    /// <param name="region">ISO alpha-2 region for a national-format number. Pass null when
    /// <paramref name="input"/> is already international — a leading '+' wins over any region.</param>
    /// <returns>False for anything libphonenumber cannot parse *or* considers invalid for its
    /// region. Parseable-but-invalid is rejected too: a well-formed number that cannot exist is
    /// still undeliverable.</returns>
    public static bool TryParse(string input, string? region, out ParsedPhone parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        try
        {
            var number = Util.Parse(input, region);
            if (!Util.IsValidNumber(number)) return false;

            var countryCode = Util.GetRegionCodeForNumber(number);
            if (string.IsNullOrEmpty(countryCode)) return false;

            parsed = new ParsedPhone(
                Util.Format(number, PhoneNumberFormat.E164),
                countryCode,
                number.NationalNumber.ToString());
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }
}
