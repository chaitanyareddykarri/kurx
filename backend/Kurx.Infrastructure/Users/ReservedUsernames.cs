using System.Text.RegularExpressions;

namespace Kurx.Infrastructure.Users;

public static partial class ReservedUsernames
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "kurx", "api", "www", "app", "support", "help",
        "login", "logout", "signup", "signin", "register", "settings", "dashboard",
        "events", "event", "orgs", "org", "organizations", "tickets", "ticket",
        "verify", "certificates", "certificate", "about", "terms", "privacy",
        "legal", "contact", "billing", "payments", "payout", "root", "system",
        "staff", "official", "team", "security", "mail", "email", "ftp", "blog",
        "docs", "status", "u", "user", "users", "me", "profile", "search",
        "explore", "home", "null", "undefined",
    };

    public static bool IsReserved(string username) => Reserved.Contains(username);

    /// <summary>Returns null if valid; error code string if invalid.</summary>
    public static string? Validate(string username)
    {
        if (!UsernameRegex().IsMatch(username)) return "invalid_username_format";
        if (username.StartsWith('.') || username.StartsWith('_')) return "invalid_username_format";
        if (username.EndsWith('.') || username.EndsWith('_')) return "invalid_username_format";
        if (username.Contains("..")) return "invalid_username_format";
        if (IsReserved(username)) return "reserved_username";
        return null;
    }

    [GeneratedRegex(@"^[a-z0-9_.]{3,30}$")]
    private static partial Regex UsernameRegex();
}
