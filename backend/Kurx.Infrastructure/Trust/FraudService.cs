using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Orgs;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Trust;

/// <summary>Fraud prevention (M13, D-052): a hard blocklist plus a risk score summed from fraud signals.
/// Identifiers are normalised (phone E.164, email lowercased, org name normalized) so lookups are exact.
/// Everything is read live so a new blacklist entry / signal takes effect on the next request.</summary>
public class FraudService(KurxDbContext db) : IFraudService
{
    public const int HighRiskThreshold = 100;

    public async Task<bool> IsBlacklistedAsync(string kind, string value, CancellationToken ct = default)
    {
        if (!Enum.TryParse<BlacklistKind>(kind, ignoreCase: true, out var k)) return false;
        var norm = Normalize(k, value);
        return await db.BlacklistEntries.AsNoTracking().AnyAsync(b => b.Kind == k && b.Value == norm, ct);
    }

    public async Task<ServiceResult<BlacklistEntryView>> AddBlacklistAsync(string kind, string value, string? reason,
        Guid createdBy, CancellationToken ct = default)
    {
        if (!Enum.TryParse<BlacklistKind>(kind, ignoreCase: true, out var k)) return ServiceResult<BlacklistEntryView>.Fail("invalid_kind");
        if (string.IsNullOrWhiteSpace(value)) return ServiceResult<BlacklistEntryView>.Fail("invalid_value");
        var norm = Normalize(k, value);

        var existing = await db.BlacklistEntries.FirstOrDefaultAsync(b => b.Kind == k && b.Value == norm, ct);
        if (existing is not null) return ServiceResult<BlacklistEntryView>.Success(ToView(existing));

        var entry = new BlacklistEntry { Kind = k, Value = norm, Reason = reason, CreatedBy = createdBy };
        db.BlacklistEntries.Add(entry);
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "admin", ActorId = createdBy,
            Action = "fraud.blacklist.add", Entity = "blacklist_entries", EntityId = entry.Id,
            DetailsJson = $"{{\"kind\":\"{k}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return ServiceResult<BlacklistEntryView>.Success(ToView(entry));
    }

    public async Task<IReadOnlyList<BlacklistEntryView>> ListBlacklistAsync(CancellationToken ct = default)
        => await db.BlacklistEntries.AsNoTracking().OrderByDescending(b => b.CreatedAt)
            .Select(b => new BlacklistEntryView(b.Id, b.Kind.ToString(), b.Value, b.Reason, b.CreatedAt))
            .ToListAsync(ct);

    public async Task<bool> RemoveBlacklistAsync(Guid id, CancellationToken ct = default)
        => await db.BlacklistEntries.Where(b => b.Id == id).ExecuteDeleteAsync(ct) > 0;

    public async Task<ServiceResult<int>> RecordSignalAsync(string subjectType, Guid subjectId, string kind,
        string? value, int score, CancellationToken ct = default)
    {
        if (!Enum.TryParse<VerificationSubjectType>(subjectType, ignoreCase: true, out var subject))
            return ServiceResult<int>.Fail("invalid_subject_type");
        if (!Enum.TryParse<FraudSignalKind>(kind, ignoreCase: true, out var k)) return ServiceResult<int>.Fail("invalid_kind");
        if (score < 0) return ServiceResult<int>.Fail("invalid_score");

        db.FraudSignals.Add(new FraudSignal { SubjectType = subject, SubjectId = subjectId, Kind = k, Value = value, Score = score });
        await db.SaveChangesAsync(ct);
        return ServiceResult<int>.Success(await SumScoreAsync(subject, subjectId, ct));
    }

    public async Task<int> GetRiskScoreAsync(string subjectType, Guid subjectId, CancellationToken ct = default)
        => Enum.TryParse<VerificationSubjectType>(subjectType, ignoreCase: true, out var subject)
            ? await SumScoreAsync(subject, subjectId, ct)
            : 0;

    public async Task<IReadOnlyDictionary<Guid, int>> GetRiskScoresBatchAsync(string subjectType, IReadOnlyList<Guid> subjectIds, CancellationToken ct = default)
    {
        if (!Enum.TryParse<VerificationSubjectType>(subjectType, ignoreCase: true, out var subject) || subjectIds.Count == 0)
            return new Dictionary<Guid, int>();
        return await db.FraudSignals.AsNoTracking()
            .Where(s => s.SubjectType == subject && subjectIds.Contains(s.SubjectId))
            .GroupBy(s => s.SubjectId)
            .Select(g => new { SubjectId = g.Key, Score = g.Sum(s => s.Score) })
            .ToDictionaryAsync(x => x.SubjectId, x => x.Score, ct);
    }

    public async Task<bool> IsUserClearAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return false;
        // Canonical column first (D-089). The lookup re-normalizes whatever it is given, and the legacy
        // bare-digit column states no country: a blacklisted Singapore number stored as "6591234567" is
        // also a well-formed Indian mobile, so re-normalizing it produced "916591234567" and matched no
        // blacklist row — the ban silently failed open for exactly the international numbers it was set on.
        if (await IsBlacklistedAsync(nameof(BlacklistKind.Phone), user.PhoneE164 ?? user.Phone, ct)) return false;
        if (!string.IsNullOrWhiteSpace(user.Email) && await IsBlacklistedAsync(nameof(BlacklistKind.Email), user.Email!, ct)) return false;
        var risk = await SumScoreAsync(VerificationSubjectType.UserIdentity, userId, ct);
        return risk < HighRiskThreshold;
    }

    private async Task<int> SumScoreAsync(VerificationSubjectType subject, Guid subjectId, CancellationToken ct)
        => await db.FraudSignals.AsNoTracking()
            .Where(s => s.SubjectType == subject && s.SubjectId == subjectId)
            .SumAsync(s => (int?)s.Score, ct) ?? 0;

    private static string Normalize(BlacklistKind kind, string value) => kind switch
    {
        BlacklistKind.Phone => AuthService.NormalizePhone(value),
        BlacklistKind.Email => value.Trim().ToLowerInvariant(),
        BlacklistKind.OrgName => OrganizationRegistryService.Normalize(value),
        _ => value.Trim().ToLowerInvariant(),
    };

    private static BlacklistEntryView ToView(BlacklistEntry b) => new(b.Id, b.Kind.ToString(), b.Value, b.Reason, b.CreatedAt);
}
