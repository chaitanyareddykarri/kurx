using System.Text.RegularExpressions;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Identity;

/// <summary>Person identity verification (M3, D-042). Runs each submission through the existing
/// <c>IKycProvider</c> (mock in dev, exactly like org bank/PAN KYC — D-016), persists ONLY masked
/// last-4 values, records every decision to <c>verification_reviews</c> (the audit spine), and caps
/// resubmission attempts. The full ID/PAN/account number is never stored.</summary>
public partial class IdentityVerificationService(KurxDbContext db, IKycProvider kyc) : IIdentityVerificationService
{
    public const int MaxSubmitAttempts = 5;

    public async Task<IdentityStatusView> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var e = await db.UserIdentities.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        return e is null
            ? new IdentityStatusView(nameof(IdentityLevel.Phone), nameof(IdentityStatus.NotStarted),
                null, null, null, null, null, null, null)
            : ToView(e);
    }

    public async Task<ServiceResult<IdentityStatusView>> SubmitGovernmentIdAsync(Guid userId, string kind,
        string idNumber, string name, CancellationToken ct = default)
    {
        kind = kind.Trim().ToLowerInvariant();
        idNumber = new string(idNumber.Where(char.IsLetterOrDigit).ToArray());
        name = name.Trim();
        // Constrained charset: kind is interpolated into the provider payload, so reject anything that
        // could break out of the JSON string (defense-in-depth even though dev provider ignores it).
        if (!GovtIdKindRegex().IsMatch(kind)) return Fail("invalid_govt_id_kind");
        if (idNumber.Length < 4) return Fail("invalid_govt_id");
        if (name.Length < 2) return Fail("invalid_name");

        var last4 = idNumber[^4..];
        return await RunAsync(userId,
            () => kyc.DigilockerAsync($"{{\"kind\":\"{kind}\",\"id_last4\":\"{last4}\"}}", ct),
            IdentityLevel.GovernmentId,
            e => { e.GovtIdKind = kind; e.GovtIdLast4 = last4; },
            component: IdentityComponent.GovernmentId,
            reasonKind: "govt_id", ct);
    }

    public async Task<ServiceResult<IdentityStatusView>> SubmitPanAsync(Guid userId, string pan, string name,
        CancellationToken ct = default)
    {
        pan = pan.Trim().ToUpperInvariant();
        name = name.Trim();
        if (!PanRegex().IsMatch(pan)) return Fail("invalid_pan");
        if (name.Length < 2) return Fail("invalid_name");

        return await RunAsync(userId,
            () => kyc.PanMatchAsync(pan, name, ct),
            IdentityLevel.GovernmentId,
            e => e.PanLast4 = pan[^4..],
            component: IdentityComponent.Pan,
            reasonKind: "pan", ct);
    }

    public async Task<ServiceResult<IdentityStatusView>> SubmitBankAsync(Guid userId, string accountNumber,
        string ifsc, string holderName, CancellationToken ct = default)
    {
        ifsc = ifsc.Trim().ToUpperInvariant();
        holderName = holderName.Trim();
        accountNumber = new string(accountNumber.Where(char.IsAsciiDigit).ToArray());
        if (accountNumber.Length is < 6 or > 20) return Fail("invalid_account_number");
        if (!IfscRegex().IsMatch(ifsc)) return Fail("invalid_ifsc");
        if (holderName.Length < 2) return Fail("invalid_name");

        // The penny drop has always been the provider call behind this submission; what changes is
        // that its outcome is now recorded as its own component state instead of being collapsed into
        // the aggregate status and lost.
        return await RunAsync(userId,
            () => kyc.PennyDropAsync(accountNumber, ifsc, holderName, ct),
            IdentityLevel.Bank,
            e =>
            {
                e.BankLast4 = accountNumber[^4..];
                e.PennyDropStatus = PennyDropStatus.Passed;
                // A provider that approves the drop has matched the holder name to do so. The mock
                // approves unconditionally, so this is only as true as the adapter behind it — which
                // is exactly why it is stored rather than inferred at read time: a real adapter sets
                // it from the response without touching a single consumer.
                e.BankNameMatch = NameMatchStatus.Match;
                e.BankVerifiedAt = DateTime.UtcNow;
            },
            component: IdentityComponent.Bank,
            reasonKind: "bank", ct,
            onReject: e =>
            {
                e.PennyDropStatus = PennyDropStatus.Failed;
                e.BankNameMatch = NameMatchStatus.Mismatch;
            });
    }

    /// <summary>Shared submission path: enforce the retry cap, call the provider, apply masked evidence
    /// on approval, promote the level, and append an automated verification_reviews row.</summary>
    private async Task<ServiceResult<IdentityStatusView>> RunAsync(Guid userId, Func<Task<KycResult>> providerCall,
        IdentityLevel approvedLevel, Action<UserIdentity> onApprove, IdentityComponent component,
        string reasonKind, CancellationToken ct, Action<UserIdentity>? onReject = null)
    {
        var e = await EnsureRowAsync(userId, ct);
        // The cap tests THIS COMPONENT, which is what the rule always meant — and what it could not
        // express while a single aggregate status existed.
        //
        // It used to read `e.Status != Approved`. That was survivable only because every submission
        // overwrote the aggregate, so a failure left it Rejected and the cap engaged. Now that the
        // aggregate is derived (Approved wins), a user who verified a government ID would carry
        // `Status == Approved` forever — and the cap would never engage again, leaving PAN and bank
        // open to unlimited guesses against the provider. Per-component state is what makes the
        // intended rule statable.
        var componentApproved = ComponentStatus(e, component) == IdentityStatus.Approved;

        // The attempt is CLAIMED in SQL before the provider is called, never read-then-incremented
        // (D-240). The old sequence read `SubmitCount`, checked it, incremented the tracked entity,
        // then awaited a network call before saving — so two concurrent submissions both read 4,
        // both passed the cap, both issued a provider call, and both wrote 5. The cap was bypassable
        // by racing it, and the provider took parallel traffic for one user. Postgres evaluates the
        // predicate under the row lock, so exactly one caller sees a claimed row.
        //
        // `e.SubmitCount` is deliberately NOT assigned here: ExecuteUpdate bypasses the change
        // tracker, and mutating the tracked value as well would make the SaveChanges below write a
        // stale literal straight back over the claim.
        if (!componentApproved)
        {
            var claimed = await db.UserIdentities
                .Where(x => x.Id == e.Id && x.SubmitCount < MaxSubmitAttempts)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.SubmitCount, x => x.SubmitCount + 1), ct);
            if (claimed == 0) return Fail("too_many_attempts");
        }

        var result = await providerCall();

        if (result.Approved)
        {
            onApprove(e);
            SetComponentStatus(e, component, IdentityStatus.Approved);
            if ((int)approvedLevel > (int)e.Level) e.Level = approvedLevel;
            e.ReviewedAt = DateTime.UtcNow;
        }
        else
        {
            onReject?.Invoke(e);
            SetComponentStatus(e, component, IdentityStatus.Rejected);
        }

        // The aggregate is now DERIVED from the components rather than overwritten by whichever
        // submission ran last. Before this, a failed bank check reported a person with an approved
        // government ID and PAN as Rejected outright — and because the capability gates read
        // component PRESENCE rather than this field, the account kept every capability while its own
        // status screen called it rejected.
        e.Status = DeriveAggregate(e);
        e.UpdatedAt = DateTime.UtcNow;

        db.VerificationReviews.Add(new VerificationReview
        {
            SubjectType = VerificationSubjectType.UserIdentity,
            SubjectId = e.Id,
            Decision = result.Approved ? VerificationDecision.Approve : VerificationDecision.Reject,
            ReviewerId = null,                       // automated / provider decision
            ReasonCode = $"{reasonKind}:{(result.Approved ? "approved" : "rejected")}",
            RiskScore = e.RiskScore,
        });

        await db.SaveChangesAsync(ct);
        return ServiceResult<IdentityStatusView>.Success(ToView(e));
    }

    private static IdentityStatus ComponentStatus(UserIdentity e, IdentityComponent component) => component switch
    {
        IdentityComponent.GovernmentId => e.GovtIdStatus,
        IdentityComponent.Pan => e.PanStatus,
        IdentityComponent.Bank => e.BankStatus,
        _ => IdentityStatus.NotStarted,
    };

    private static void SetComponentStatus(UserIdentity e, IdentityComponent component, IdentityStatus status)
    {
        switch (component)
        {
            case IdentityComponent.GovernmentId: e.GovtIdStatus = status; break;
            case IdentityComponent.Pan: e.PanStatus = status; break;
            case IdentityComponent.Bank: e.BankStatus = status; break;
        }
    }

    /// <summary>The aggregate is the best state any component has reached, not the most recent one.
    /// Approved wins because a person who has proved their government ID HAS proved it — a later
    /// failed bank attempt does not un-prove it. Rejected surfaces only when something was tried and
    /// nothing succeeded, which is the one case where it describes the person accurately.</summary>
    private static IdentityStatus DeriveAggregate(UserIdentity e)
    {
        var components = new[] { e.GovtIdStatus, e.PanStatus, e.BankStatus };
        if (components.Any(x => x == IdentityStatus.Approved)) return IdentityStatus.Approved;
        if (components.Any(x => x == IdentityStatus.Rejected)) return IdentityStatus.Rejected;
        if (components.Any(x => x is IdentityStatus.Submitted or IdentityStatus.UnderReview))
            return IdentityStatus.Submitted;
        return IdentityStatus.NotStarted;
    }

    /// <summary>Idempotent get-or-create isolating the concurrent first-submission race on the unique
    /// UserId (two simultaneous requests both find no row): the loser's insert 23505s, and we re-fetch
    /// the winner instead of failing — the same pattern AuthService uses for first-login (D-037).</summary>
    private async Task<UserIdentity> EnsureRowAsync(Guid userId, CancellationToken ct)
    {
        var e = await db.UserIdentities.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (e is not null) return e;
        e = new UserIdentity { UserId = userId };
        db.UserIdentities.Add(e);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true)
        {
            db.ChangeTracker.Clear();
            e = await db.UserIdentities.FirstAsync(x => x.UserId == userId, ct);
        }
        return e;
    }

    private static ServiceResult<IdentityStatusView> Fail(string error) => ServiceResult<IdentityStatusView>.Fail(error);

    private static IdentityStatusView ToView(UserIdentity e) => new(
        e.Level.ToString(), e.Status.ToString(),
        e.GovtIdKind, e.GovtIdLast4, e.PanLast4, e.BankLast4,
        e.ReviewedAt, e.ExpiresAt, e.UpdatedAt,
        e.GovtIdStatus.ToString(), e.PanStatus.ToString(), e.BankStatus.ToString(),
        e.PennyDropStatus.ToString(), e.BankNameMatch.ToString(), e.BankVerifiedAt);

    /// <summary>Reads the trail <see cref="RunAsync"/> has been writing since M3. Scoped to the
    /// caller by user id rather than accepting a subject id: a verification decision is a private
    /// fact about a person, and an id-addressed history would be a disclosure surface.</summary>
    public async Task<IReadOnlyList<IdentityHistoryEntry>> GetHistoryAsync(Guid userId, int limit = 50,
        CancellationToken ct = default)
    {
        var identityId = await db.UserIdentities.AsNoTracking()
            .Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        // No identity row means nothing was ever submitted — an empty history, not an error.
        if (identityId is not { } subjectId) return [];

        var rows = await db.VerificationReviews.AsNoTracking()
            .Where(r => r.SubjectType == VerificationSubjectType.UserIdentity && r.SubjectId == subjectId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            // Reason codes are written as "component:decision" by RunAsync. Split defensively — a row
            // predating that convention, or a hand-written admin note, must still render as history
            // rather than throw the whole list away.
            var parts = (r.ReasonCode ?? "").Split(':', 2);
            return new IdentityHistoryEntry(
                parts.Length == 2 ? parts[0] : "unknown",
                r.Decision.ToString().ToLowerInvariant(),
                r.ReviewerId,
                r.ReasonCode,
                r.Notes,
                r.CreatedAt);
        }).ToList();
    }

    [GeneratedRegex("^[a-z_]{2,40}$")]
    private static partial Regex GovtIdKindRegex();

    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")]
    private static partial Regex PanRegex();

    [GeneratedRegex("^[A-Z]{4}0[A-Z0-9]{6}$")]
    private static partial Regex IfscRegex();
}
