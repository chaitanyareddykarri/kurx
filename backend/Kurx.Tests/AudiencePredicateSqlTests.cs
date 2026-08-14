using Kurx.Application.Abstractions;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Tests;

/// <summary>Pins the query-side mirror of the audience floor (D-272).
///
/// <para><c>PostService.VisibleTo</c> cannot resolve one caller per row, so the audience rule is <b>also</b>
/// expressed as an EF predicate over <see cref="EventAuthority.AudienceRoles"/>. Two things could silently
/// break it: the role array drifting from <c>LevelFor</c>, and the array failing to reach SQL — which would
/// turn a security filter into client-side evaluation over an unfiltered result set.</para>
///
/// <para><b>Deliberately fixture-free.</b> <c>ToQueryString()</c> compiles the expression tree through the
/// Npgsql provider without opening a connection, so this needs no database and cannot perturb the
/// order-sensitive integration classes. The behavioural half — that a <c>Staff</c> seat sees a
/// participants-only post and a <c>Finance</c> seat gets 404 — is
/// <see cref="EventAudienceAuthorizationTests"/>'s job, over real Postgres.</para></summary>
public class AudiencePredicateSqlTests
{
    private static KurxDbContext NewContext() => new(
        new DbContextOptionsBuilder<KurxDbContext>()
            .UseNpgsql("Host=localhost;Database=kurx_translation_probe;Username=kurx;Password=kurx")
            .Options);

    /// <summary>The derived arrays must agree with <c>LevelFor</c> — the whole reason they are computed
    /// rather than typed out. A hand-edited list would pass every other test in the suite.</summary>
    [Fact]
    public void The_query_side_role_sets_are_derived_from_LevelFor_and_exclude_Finance()
    {
        Assert.All(EventAuthority.AudienceRoles,
            r => Assert.True(EventAuthority.LevelFor(r) >= EventAuthorityLevel.Participant));
        Assert.All(EventAuthority.ModeratorRoles,
            r => Assert.True(EventAuthority.LevelFor(r) >= EventAuthorityLevel.Staff));

        // …and are COMPLETE, not merely sound: every role the ladder admits must be present, or the
        // predicate would silently under-grant where the resolver grants.
        Assert.Equal(
            Enum.GetValues<OrgRole>().Where(r => EventAuthority.LevelFor(r) >= EventAuthorityLevel.Participant).Order(),
            EventAuthority.AudienceRoles.Order());

        // The seat that must never appear on either — money authority is not event authority.
        Assert.DoesNotContain(OrgRole.Finance, EventAuthority.AudienceRoles);
        Assert.DoesNotContain(OrgRole.Finance, EventAuthority.ModeratorRoles);
    }

    /// <summary>EF Core 3.0+ throws rather than client-evaluating a <c>Where</c>, so <c>ToQueryString()</c>
    /// returning at all is the translation guarantee. The assertions below pin <i>what</i> reached the
    /// server: were the role array evaluated in memory, no membership predicate would appear.</summary>
    [Fact]
    public void The_audience_predicate_translates_to_server_side_SQL()
    {
        using var db = NewContext();
        var me = Guid.NewGuid();

        var sql = db.Posts.AsNoTracking().Where(p =>
            p.Visibility == PostVisibility.EventParticipants && p.EventId != null
            && db.Events.Any(e => e.Id == p.EventId && e.DeletedAt == null
                && (e.CreatedBy == me
                    || db.Memberships.Any(m => m.OrgId == e.RepresentingOrgId && m.UserId == me
                        && EventAuthority.AudienceRoles.Contains(m.Role))
                    || db.EventParticipants.Any(ep => ep.EventId == e.Id
                        && ep.SubjectType == ParticipantSubjectType.Person
                        && ep.SubjectId == me && ep.State == ParticipantState.Active)
                    || db.Tickets.Any(t => t.EventId == e.Id && t.UserId == me && t.State != TicketState.Void))))
            .ToQueryString();

        Console.WriteLine("─── GENERATED SQL ───");
        Console.WriteLine(sql);
        Console.WriteLine("─── END SQL ───");

        // All four arms of the resolver reached the server (tables are snake_case, columns quoted PascalCase).
        Assert.Contains("FROM memberships", sql, StringComparison.Ordinal);
        Assert.Contains("FROM event_participants", sql, StringComparison.Ordinal);
        Assert.Contains("FROM tickets", sql, StringComparison.Ordinal);
        Assert.Contains("\"CreatedBy\" = @me", sql, StringComparison.Ordinal);

        // The load-bearing line: the role set is a native IN list evaluated by Postgres, not a filter
        // applied in memory afterwards — and Finance is absent from it. If EF ever stops translating this,
        // `Contains` would fall back to fetching every membership row and filtering client-side, which
        // turns an authorization predicate into a suggestion.
        Assert.Contains("m.\"Role\" IN ('Owner', 'Manager', 'Staff', 'Representative')", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("Finance", sql, StringComparison.Ordinal);
    }
}
