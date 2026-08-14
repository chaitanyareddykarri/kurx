using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Users;

/// <summary>
/// Account settings (D-263).
///
/// <para>Nothing here invents a credential mechanism. The email ceremony delegates to
/// <see cref="IRegistrationService"/>, which already owns email OTP; the phone ceremony goes through
/// <see cref="IOtpService"/> with the same canonicalizer auth uses; recovery and step-up are untouched.
/// A second OTP path would be a second set of attempt caps to get wrong.</para>
/// </summary>
public class AccountService(
    KurxDbContext db,
    IRegistrationService registration,
    INotificationService notifications,
    IAuditWriter audit) : IAccountService
{
    /// <summary>Grace window before an account is anonymised. 30 days is the span the deletion contract
    /// advertises to the user, so it lives here rather than in configuration where the two could drift.</summary>
    public const int DeletionGraceDays = 30;

    // ── Notification preferences ───────────────────────────────────────────────

    public async Task<NotificationPreferencesView> GetNotificationPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        var stored = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId).ToListAsync(ct);

        var rows = NotificationCategories.All.Select(category =>
        {
            var row = stored.FirstOrDefault(p => p.Category == category);
            var locked = NotificationCategories.IsAlwaysDelivered(category);
            return new NotificationCategoryPreferenceView(
                NotificationCategories.Wire(category),
                // A locked category always reads as fully on, whatever a stale row says — the read has to
                // agree with what dispatch will actually do.
                InApp: locked || (row?.InApp ?? true),
                Push: locked || (row?.Push ?? true),
                Email: locked || (row?.Email ?? true),
                WhatsApp: locked || (row?.WhatsApp ?? false),
                Locked: locked);
        }).ToList();

        return new NotificationPreferencesView(rows);
    }

    public async Task<ServiceResult<NotificationPreferencesView>> UpdateNotificationPreferencesAsync(
        Guid userId, IReadOnlyList<NotificationPreferenceInput> changes, CancellationToken ct = default)
    {
        if (changes is null || changes.Count == 0)
            return ServiceResult<NotificationPreferencesView>.Fail("no_changes");

        var parsed = new List<(NotificationCategory Category, NotificationPreferenceInput Input)>();
        foreach (var change in changes)
        {
            var category = NotificationCategories.Parse(change.Category);
            if (category is null) return ServiceResult<NotificationPreferencesView>.Fail("invalid_category");

            // Refuse rather than silently ignore. A screen that appears to accept a change it discards
            // is worse than one that says no — the user believes they are no longer being notified.
            if (NotificationCategories.IsAlwaysDelivered(category.Value)
                && (change.InApp == false || change.Push == false || change.Email == false || change.WhatsApp == false))
                return ServiceResult<NotificationPreferencesView>.Fail("category_not_optional");

            parsed.Add((category.Value, change));
        }

        var existing = await db.NotificationPreferences
            .Where(p => p.UserId == userId).ToListAsync(ct);

        foreach (var (category, input) in parsed)
        {
            var row = existing.FirstOrDefault(p => p.Category == category);
            if (row is null)
            {
                // Materialise from the DEFAULT, not from all-false, so a PATCH naming one channel does
                // not silently switch the other three off for a user who had no row.
                row = new NotificationPreference { UserId = userId, Category = category, WhatsApp = false };
                db.NotificationPreferences.Add(row);
                existing.Add(row);
            }
            if (input.InApp.HasValue) row.InApp = input.InApp.Value;
            if (input.Push.HasValue) row.Push = input.Push.Value;
            if (input.Email.HasValue) row.Email = input.Email.Value;
            if (input.WhatsApp.HasValue) row.WhatsApp = input.WhatsApp.Value;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult<NotificationPreferencesView>.Success(await GetNotificationPreferencesAsync(userId, ct));
    }

    // ── Blocks ─────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> BlockAsync(Guid userId, Guid targetUserId, CancellationToken ct = default)
    {
        if (userId == targetUserId) return ServiceResult<bool>.Fail("cannot_block_self");
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == targetUserId, ct))
            return ServiceResult<bool>.Fail("not_found");

        db.UserBlocks.Add(new UserBlock { BlockerId = userId, BlockedId = targetUserId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Already blocked — the unique pair index, not an application check, is what makes this
            // idempotent under a double tap.
            db.ChangeTracker.Clear();
            return ServiceResult<bool>.Success(true);
        }

        // A block ends any pending ally relationship in either direction: leaving a request outstanding
        // would keep the blocked party in the other's inbox, which is the thing being asked to stop.
        await db.AllyConnections
            .Where(a => (a.UserLowId == userId && a.UserHighId == targetUserId)
                     || (a.UserHighId == userId && a.UserLowId == targetUserId))
            .Where(a => a.Status == AllyStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, AllyStatus.Declined)
                .SetProperty(a => a.RespondedAt, DateTimeOffset.UtcNow), ct);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<bool> UnblockAsync(Guid userId, Guid targetUserId, CancellationToken ct = default)
        => await db.UserBlocks.Where(b => b.BlockerId == userId && b.BlockedId == targetUserId)
            .ExecuteDeleteAsync(ct) > 0;

    public async Task<IReadOnlyList<BlockedUserView>> ListBlocksAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.UserBlocks.AsNoTracking()
            .Where(b => b.BlockerId == userId)
            .Join(db.Users.AsNoTracking(), b => b.BlockedId, u => u.Id,
                (b, u) => new { b.CreatedAt, u.Id, u.Name, u.Username, u.AvatarKey })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(r => new BlockedUserView(r.Id, r.Name, r.Username, r.AvatarKey, r.CreatedAt)).ToList();
    }

    // ── Username history ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<UsernameHistoryEntryView>> GetUsernameHistoryAsync(Guid userId, CancellationToken ct = default)
        => await db.UsernameHistory.AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ReleasedAt)
            .Select(h => new UsernameHistoryEntryView(h.Username, h.ReleasedAt))
            .ToListAsync(ct);

    // ── Email change ───────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> StartEmailChangeAsync(Guid userId, string newEmail, string? requestIp, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return ServiceResult<bool>.Fail("not_found");

        // Delegated: RegistrationService already owns email OTP issuance, uniqueness and normalisation.
        var started = await registration.StartEmailVerificationAsync(userId, newEmail, requestIp, ct);
        if (!started.Ok) return ServiceResult<bool>.Fail(started.Error ?? "invalid_request");

        // The OLD address is told a change was requested. Sent on START, because on an account that is
        // already compromised this is the only signal the real owner ever gets, and after the change
        // completes we no longer have an address to warn.
        if (!string.IsNullOrWhiteSpace(user.Email))
            await NotifySafeAsync(userId, "email_change_requested", "Email change requested",
                $"Someone requested changing the email on your Kurx account to {Mask(newEmail)}. " +
                "If this wasn't you, secure your account now.", ct);

        audit.Write(new AuditEvent("account.email_change_started", "users", userId, ActorId: userId));
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<EmailChangeView>> CompleteEmailChangeAsync(Guid userId, string newEmail, string code, CancellationToken ct = default)
    {
        var completed = await registration.CompleteEmailVerificationAsync(userId, newEmail, code, ct);
        if (!completed.Ok) return ServiceResult<EmailChangeView>.Fail(completed.Error ?? "invalid_request");

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        return ServiceResult<EmailChangeView>.Success(new EmailChangeView(user.Email, user.EmailVerifiedAt is not null));
    }

    // ── Deletion (D-263) ───────────────────────────────────────────────────────

    public async Task<AccountDeletionView?> GetDeletionAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DeletionScheduledFor, u.DeletionRequestedAt })
            .FirstOrDefaultAsync(ct);
        if (user?.DeletionScheduledFor is null) return null;
        return new AccountDeletionView(true, user.DeletionScheduledFor, user.DeletionRequestedAt);
    }

    public async Task<ServiceResult<AccountDeletionView>> RequestDeletionAsync(Guid userId, string? reason, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return ServiceResult<AccountDeletionView>.Fail("not_found");
        if (user.DeletionScheduledFor is not null)
            return ServiceResult<AccountDeletionView>.Success(
                new AccountDeletionView(true, user.DeletionScheduledFor, user.DeletionRequestedAt));

        user.DeletionRequestedAt = DateTime.UtcNow;
        user.DeletionScheduledFor = DateTime.UtcNow.AddDays(DeletionGraceDays);
        user.DeletionReason = reason?.Trim();

        db.SecurityEvents.Add(new SecurityEvent { UserId = userId, Type = "account.deletion_requested", Severity = "warning" });
        audit.Write(new AuditEvent("account.deletion_requested", "users", userId, ActorId: userId,
            After: new { scheduled_for = user.DeletionScheduledFor }));
        await db.SaveChangesAsync(ct);

        await NotifySafeAsync(userId, "account_deletion_requested", "Account deletion scheduled",
            $"Your Kurx account is scheduled for deletion on {user.DeletionScheduledFor:d MMM yyyy}. " +
            "You can cancel any time before then by signing in.", ct);

        return ServiceResult<AccountDeletionView>.Success(
            new AccountDeletionView(true, user.DeletionScheduledFor, user.DeletionRequestedAt));
    }

    public async Task<bool> CancelDeletionAsync(Guid userId, CancellationToken ct = default)
    {
        // Guarded on the grace window still being open, evaluated under the row lock: a cancel that
        // arrives while the sweep is anonymising must not resurrect a half-cleared account.
        var cancelled = await db.Users
            .Where(u => u.Id == userId && u.DeletionScheduledFor != null && u.AnonymizedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.DeletionScheduledFor, (DateTime?)null)
                .SetProperty(u => u.DeletionRequestedAt, (DateTime?)null)
                .SetProperty(u => u.DeletionReason, (string?)null), ct);
        return cancelled > 0;
    }

    public async Task<int> RunScheduledDeletionsAsync(CancellationToken ct = default)
    {
        var due = await db.Users
            .Where(u => u.DeletionScheduledFor != null && u.DeletionScheduledFor <= DateTime.UtcNow && u.AnonymizedAt == null)
            .Take(100)
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        foreach (var user in due)
        {
            // The user's own speech goes. Their financial and certificate records do not — those carry
            // statutory retention, and an audit log the audited party can erase is not an audit log
            // (D-263). Both sets keep pointing at this id, which is now an opaque handle.
            await db.Posts.Where(p => p.AuthorId == user.Id && !p.IsDeleted)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true), ct);
            await db.PostComments.Where(c => c.AuthorId == user.Id && !c.IsDeleted)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true), ct);

            var anonymized = $"deleted-{user.Id:N}"[..24];
            user.Name = "Deleted user";
            user.Phone = anonymized;
            user.PhoneE164 = null;
            user.PhoneNational = null;
            user.CountryCode = null;
            user.Email = null;
            user.EmailVerifiedAt = null;
            user.Username = null;
            user.AvatarKey = null;
            user.CoverKey = null;
            user.Headline = null;
            user.Bio = null;
            user.Skills = null;
            user.LinksJson = null;
            user.EducationJson = null;
            user.SectionVisibilityJson = null;
            user.ProfilePublic = false;
            user.AnonymizedAt = DateTime.UtcNow;
            user.DeletionScheduledFor = null;

            audit.Write(new AuditEvent("account.anonymized", "users", user.Id, ActorType: "system"));
        }

        await db.SaveChangesAsync(ct);
        return due.Count;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Security notices must never fail the action that triggered them — the change is already
    /// committed by the time this runs, and a dead notification channel is not a reason to report failure.</summary>
    private async Task NotifySafeAsync(Guid userId, string kind, string title, string body, CancellationToken ct)
    {
        try
        {
            await notifications.NotifyAsync(userId, kind, title, body,
                new { notificationType = "security", route = "/settings/security", deep_link = "/settings/security" }, ct);
        }
        catch
        {
            // Swallowed by design.
        }
    }

    /// <summary>Shows enough of the destination to recognise it, not enough to learn it. The warning goes
    /// to an address that may already be attacker-controlled.</summary>
    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return "•••";
        return email[0] + new string('•', Math.Min(at - 1, 6)) + email[at..];
    }
}
