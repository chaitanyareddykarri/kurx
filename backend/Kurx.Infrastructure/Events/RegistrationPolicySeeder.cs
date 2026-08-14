using System.Text.Json;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurx.Infrastructure.Events;

/// <summary>D-266 M3 Step 2 — gives every Type node an explicit
/// <c>AllowedRegistrationPoliciesJson</c>. No type supports all seven policies.
///
/// <para><b>Derived from the archetype, not written per type.</b> Listing 145 rows by hand would be exactly
/// the hardcoded-event-type logic the architecture forbids, and it would drift the first time a type was
/// added. The archetype default is the rule; <see cref="Overrides"/> holds only the types whose behaviour
/// genuinely differs from their archetype, and each one states why. The result is still an explicit
/// per-type list in the database — derivation is how it is computed, not how it is stored.</para>
///
/// <para>Idempotent and drift-detecting: a row whose stored list no longer matches the derivation is
/// corrected, because this seeding is the transcription of a frozen decision. Admin edits after first boot
/// are the reason this compares rather than blindly overwrites — see the note on re-sync below.</para></summary>
public static class RegistrationPolicySeeder
{
    private const EventRegistrationPolicy Open = EventRegistrationPolicy.Open;
    private const EventRegistrationPolicy Invite = EventRegistrationPolicy.InviteOnly;
    private const EventRegistrationPolicy Approval = EventRegistrationPolicy.ApprovalRequired;
    private const EventRegistrationPolicy College = EventRegistrationPolicy.CollegeRestricted;
    private const EventRegistrationPolicy Dept = EventRegistrationPolicy.DepartmentRestricted;
    private const EventRegistrationPolicy Alumni = EventRegistrationPolicy.AlumniOnly;
    private const EventRegistrationPolicy Verified = EventRegistrationPolicy.VerifiedOnly;

    /// <summary>Archetype → what its events may use. A Private archetype is Invite-only by product rule
    /// (<see cref="PolicyResolver"/> enforces it regardless), stated here so the stored data agrees.</summary>
    private static readonly Dictionary<string, EventRegistrationPolicy[]> ByArchetype = new(StringComparer.Ordinal)
    {
        ["competitive"] = [Open, Invite, Approval, College, Dept, Verified],
        ["learning"] = [Open, Invite, Approval, College, Dept],
        ["conference"] = [Open, Invite, Approval, Verified],
        ["exhibition"] = [Open, Invite, Approval],
        ["tournament"] = [Open, Invite, Approval, College, Dept],
        ["endurance"] = [Open, Invite, Approval],
        // Recruitment is never open to the public: a hiring drive is scoped to a cohort by definition.
        ["recruitment"] = [College, Dept, Invite, Approval, Alumni],
        ["performance"] = [Open, Invite],
        ["community"] = [Open, Invite, Approval, College, Alumni],
        // Civic service is Open only — an approval queue or an institution gate on a blood drive would
        // turn a public-benefit activity into a private one.
        ["civic"] = [Open],
        ["fundraising"] = [Open, Invite],
        ["festival"] = [Open, Invite, Approval, College],
        ["ceremonial"] = [Open, Invite, College, Dept, Alumni],
        ["private-gathering"] = [Invite],
    };

    /// <summary>Types whose behaviour differs from their archetype's default. Each entry is a product
    /// decision, not a convenience.</summary>
    private static readonly Dictionary<string, EventRegistrationPolicy[]> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // One employer, one cohort, interview slots — never an open sign-up.
        ["Campus Recruitment"] = [College, Invite],
        ["Placement Drive"] = [College, Invite],
        // A careers fair is the browse-many-employers case, so it stays open to the whole cohort.
        ["Career Fair"] = [College, Open, Approval],
        // Alumni events check one fact and only that fact.
        ["Alumni Meet"] = [Alumni, Invite],
        // Donation collection must not be gated: gating a fundraiser suppresses the donations.
        ["Fundraiser"] = [Open],
        ["Blood Drive"] = [Open],
        ["Blood Donation Camp"] = [Open],
        // Department-scoped by name; the department IS the audience.
        ["Department Event"] = [Dept, College, Invite],
    };

    private static readonly JsonSerializerOptions Json = new();

    public static async Task SeedAsync(KurxDbContext db, CancellationToken ct = default)
    {
        var types = await db.EventCategories
            .Where(c => c.Level == CategoryLevel.Type)
            .ToListAsync(ct);

        var dirty = false;
        foreach (var t in types)
        {
            var want = Resolve(t.Name, t.ArchetypeSlug, t.ProductClass);
            if (want is null) continue;   // unmapped archetype — leave null rather than guess

            var json = JsonSerializer.Serialize(want.Select(p => p.ToString()).ToArray(), Json);
            if (t.AllowedRegistrationPoliciesJson == json) continue;
            t.AllowedRegistrationPoliciesJson = json;
            dirty = true;
        }
        if (dirty) await db.SaveChangesAsync(ct);
    }

    /// <summary>Exposed for tests: the allow-list a type should carry. Product wins over the archetype
    /// default, mirroring <see cref="PolicyResolver.AllowedFor"/> so the stored data can never contradict
    /// what the resolver would compute.</summary>
    public static EventRegistrationPolicy[]? Resolve(string typeName, string? archetypeSlug, EventProduct? product)
    {
        if (product == EventProduct.Private) return [Invite];
        if (Overrides.TryGetValue(typeName, out var o)) return o;
        if (archetypeSlug is null) return null;
        return ByArchetype.GetValueOrDefault(archetypeSlug);
    }
}
