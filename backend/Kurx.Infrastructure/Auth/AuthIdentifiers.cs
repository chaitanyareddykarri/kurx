using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Auth;

/// <summary>Resolves a user-supplied sign-in identifier (phone or email) to an account. Shared by every
/// pre-session entry point — device-approval login (AM4), passkey login (AM3) and account recovery (AM7) —
/// because if they disagreed about what an identifier matches, one surface would leak the existence of
/// accounts the other denies.
///
/// <para><b>Dual-read (D-089, phase 3).</b> While the staged E.164 migration is in flight a user may have
/// a canonical <c>PhoneE164</c>, only a legacy <c>Phone</c>, or both. Resolution tries the canonical
/// column first and falls back to legacy digits, so a half-backfilled table authenticates everyone. This
/// is what makes the migration zero-downtime: neither column is load-bearing on its own.</para></summary>
public static class AuthIdentifiers
{
    public static async Task<User?> ResolveUserAsync(KurxDbContext db, string identifier, CancellationToken ct)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Contains('@'))
        {
            var email = trimmed.ToLowerInvariant();
            return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);
        }

        // Phone before username, because a phone is the more specific shape: only a string that is
        // *entirely* phone characters and of plausible E.164 length is treated as one.
        if (LooksLikePhone(trimmed))
        {
            var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length is >= 8 and <= 15)
            {
                var e164 = "+" + digits;
                // One query over both representations — a caller may send "+919876543210",
                // "919876543210" or "09876543210" and must land on the same account regardless of how
                // far the E.164 backfill (D-089) has got.
                var byPhone = await db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.PhoneE164 == e164 || u.Phone == digits, ct);
                if (byPhone is not null) return byPhone;
            }
        }

        // Username is the fallback rather than a parallel branch, so an all-numeric username still
        // resolves after the phone lookup misses. Usernames are stored lowercase (D-011).
        var username = trimmed.ToLowerInvariant();
        return await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username != null && u.Username == username, ct);
    }

    /// <summary>True when every character is one a phone number may legitimately contain. Deliberately
    /// strict: "alice2" contains digits but is not a phone, and misreading it as one would send the
    /// lookup down the wrong column and fail a legitimate sign-in.</summary>
    private static bool LooksLikePhone(string value) =>
        value.Length > 0 && value.All(c => char.IsAsciiDigit(c) || c is '+' or '-' or ' ' or '(' or ')');
}
