namespace Kurx.Infrastructure.Configuration;

/// <summary>
/// Shared guard for config values that must be replaced before a real deployment.
/// Dev placeholder values are intentionally committed (appsettings.Development.json,
/// .env.example, docker-compose.yml) so the app runs zero-config locally; this stops
/// the same placeholders from silently reaching a Production environment.
/// </summary>
public static class SecretValidation
{
    private static readonly HashSet<string> KnownDevValues = new(StringComparer.Ordinal)
    {
        "dev-only-secret-change-me-0123456789abcdef",
        "dev-only-ticket-hmac-change-me",
        // Committed in .env.example, docker-compose.yml and KurxApiFactory. Its absence is already
        // fatal, but a WEAK value was not: OTP hashes computed under a guessable pepper look
        // completely healthy (D-115), so this is the one secret whose misconfiguration nothing
        // downstream would ever surface.
        "dev-only-otp-pepper-change-me",
    };

    /// <summary>Throws if <paramref name="value"/> is too short or is a known committed dev placeholder.</summary>
    public static void RequireStrongProductionSecret(string name, string value, int minLength = 32)
    {
        // Placeholder first, length second. Several committed placeholders are also short, and
        // "must be at least 32 characters" sends an operator off to lengthen the dev value rather than
        // replace it. Naming the actual mistake is the more actionable failure.
        if (KnownDevValues.Contains(value))
            throw new InvalidOperationException(
                $"{name} is still set to its committed development placeholder value. " +
                "Set a unique secret via environment variable before running in Production.");
        if (value.Length < minLength)
            throw new InvalidOperationException(
                $"{name} must be at least {minLength} characters in Production (got {value.Length}).");
    }

    /// <summary>Throws if a Postgres connection string still carries the committed dev password.</summary>
    public static void RequireProductionConnectionString(string name, string value)
    {
        if (value.Contains("Password=kurx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{name} is still using the committed development password. " +
                "Set a real Postgres connection string via environment variable before running in Production.");
    }
}
