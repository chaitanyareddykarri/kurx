using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurx.Tests;

/// <summary>
/// D-388 — put a live event momentarily back in <c>Draft</c> so a suite whose subject is something else
/// can still write its fixture.
///
/// <para>D-388 froze an event's protected substance once it is a Public product that is publicly live:
/// ticket types, audience rules and the authorization letter all answer <c>change_request_required</c>
/// from <see cref="EventStatusWorkflow.IsLiveProtected"/>. That is the right rule. What it collided with
/// is a large body of tests whose fixtures publish first — because the thing under test (registration,
/// ordering, transfers, check-in) only happens on a live event — and then configure. Those tests began
/// failing on their SETUP, not on their subject.</para>
///
/// <para><b>Why this rather than seeding rows directly.</b> The write still goes through the real
/// endpoint, so its authorization, validation and canonicalisation stay under test — which is precisely
/// where these suites have caught defects before. Only the incidental dependency on "the event happened
/// to be live at the moment the fixture was written" is removed. A test whose subject IS the live-edit
/// rule must not use this: assert <c>change_request_required</c> directly, or drive the change-request
/// flow.</para>
///
/// <para>Extracted from <c>AudienceRuleTests</c>, which established this shape first and holds a private
/// copy. Eleven suites need it, so it lives once here; that copy can migrate to this whenever its owner
/// next touches the file.</para>
/// </summary>
public static class LiveStatusSuspension
{
    /// <summary>Runs <paramref name="write"/> with the event temporarily out of its live-protected
    /// status, restoring the status afterwards even if the write throws.</summary>
    public static async Task<T> WithLiveStatusSuspendedAsync<T>(
        this KurxApiFactory factory, Guid eventId, Func<Task<T>> write)
    {
        var restore = await SuspendAsync(factory, eventId);
        try { return await write(); }
        finally { await RestoreAsync(factory, eventId, restore); }
    }

    /// <summary>The status the event had, or null when it was not live-protected — in which case nothing
    /// was changed and nothing is restored.</summary>
    private static async Task<EventStatus?> SuspendAsync(KurxApiFactory factory, Guid eventId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null || !EventStatusWorkflow.IsLiveProtected(ev.Product, ev.Status)) return null;

        var was = ev.Status;
        ev.Status = EventStatus.Draft;
        await db.SaveChangesAsync();
        return was;
    }

    private static async Task RestoreAsync(KurxApiFactory factory, Guid eventId, EventStatus? status)
    {
        if (status is not { } s) return;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KurxDbContext>();
        var ev = await db.Events.FirstAsync(e => e.Id == eventId);
        ev.Status = s;
        await db.SaveChangesAsync();
    }
}
