using Kurx.Application.Abstractions;

namespace Kurx.Infrastructure.Auth;

/// <summary>Password policy per <b>NIST SP 800-63B §5.1.1</b> (D-129).
///
/// <para><b>What this deliberately does NOT do.</b> No composition rules — no "must contain an uppercase
/// letter and a symbol". NIST dropped them because they measurably reduce entropy in practice: told to
/// add a symbol, users produce <c>Password1!</c>, not something stronger. Length plus a breach check is
/// what actually correlates with resistance to guessing. There is also no forced expiry: rotation on a
/// calendar makes users pick incrementing variants, so rotation should be triggered by evidence of
/// compromise instead.</para></summary>
public static class PasswordPolicy
{
    /// <summary>NIST floor is 8; 12 is chosen because the credential also guards money movement and PII
    /// on this platform, and the memorised-secret is only factor 1 of three.</summary>
    public const int MinLength = 12;

    /// <summary>Accepting long passphrases is required by NIST. The cap exists only to bound the work an
    /// unauthenticated caller can force the hasher to do — Argon2id cost scales with input handling, so
    /// an unbounded field is a cheap denial-of-service lever.</summary>
    public const int MaxLength = 128;

    /// <summary>Refused outright. A compact, high-signal deny list rather than a full breach corpus:
    /// these are the passwords that dominate real credential-stuffing lists, and every one of them
    /// satisfies a naive "12 characters with a number and a symbol" rule.
    ///
    /// <para>This is a floor, not the finished control — see D-129 for the deferred Have I Been Pwned
    /// k-anonymity range check, which covers the long tail this list cannot.</para></summary>
    private static readonly HashSet<string> Breached = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "password1234", "password12345",
        "passw0rd123", "p@ssw0rd", "p@ssword123", "passwordpassword",
        "123456789012", "1234567890123", "12345678901234", "111111111111",
        "qwertyuiop123", "qwerty123456", "1q2w3e4r5t6y", "qazwsxedcrfv",
        "letmein12345", "welcome12345", "iloveyou1234", "admin1234567",
        "administrator", "changeme1234", "trustno1trustno1", "monkey123456",
        "football1234", "baseball1234", "dragon123456", "sunshine1234",
        "princess1234", "superman1234", "michael12345", "shadow123456",
        "master123456", "abc123abc123", "aaaaaaaaaaaa", "asdfghjkl123",
        "zaq12wsxcde3", "qwertyuiopasdfgh", "secret123456", "default12345",
    };

    /// <summary>Validates a candidate. Order matters only for which message the user sees first; every
    /// rule is independent.</summary>
    public static PasswordRejection Validate(string password, string? username, string? email, string? phone)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength) return PasswordRejection.TooShort;
        if (password.Length > MaxLength) return PasswordRejection.TooLong;
        if (Breached.Contains(password.Trim())) return PasswordRejection.Breached;
        if (DerivedFromIdentifier(password, username, email, phone)) return PasswordRejection.ContainsIdentifier;
        return PasswordRejection.None;
    }

    /// <summary>Blocks passwords built from the user's own identifiers. These are the first candidates any
    /// targeted guess tries, and they survive a pure length check comfortably
    /// (<c>alice@example.com</c> is 17 characters).</summary>
    private static bool DerivedFromIdentifier(string password, string? username, string? email, string? phone)
    {
        // The phone is checked in both representations. A stored value may be canonical E.164
        // ("+919876543210") while the user types the digits alone into the password field — comparing
        // only the stored form would let "919876543210xyz" straight through, because the leading '+'
        // is absent from the password.
        foreach (var identifier in new[] { username, email, LocalPartOf(email), phone, DigitsOf(phone) })
        {
            // Short identifiers are skipped: refusing every password containing a 3-letter username
            // would reject far more good passwords than bad ones.
            if (string.IsNullOrWhiteSpace(identifier) || identifier.Length < 4) continue;
            if (password.Contains(identifier, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>Digits-only form of a phone number, so a canonical <c>+91…</c> value still matches a
    /// password containing the bare national/international digits.</summary>
    private static string? DigitsOf(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 4 ? digits : null;
    }

    private static string? LocalPartOf(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : null;
    }
}
