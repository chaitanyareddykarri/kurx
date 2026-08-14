using Kurx.Domain.Enums;
using Kurx.Infrastructure.Events;
using static Kurx.Infrastructure.Events.PolicyResolver;

namespace Kurx.Tests;

/// <summary>D-266 M3 Step 1 — the canonical policy pipeline. Pure resolution, no database: the point is
/// that backend, admin, web and Flutter all get the same answer, which only holds if the decision lives in
/// one deterministic function.</summary>
public class PolicyResolverTests
{
    private static readonly EventRegistrationPolicy[] AllPolicies = Enum.GetValues<EventRegistrationPolicy>();

    // ── Step 1 · product outranks everything below it ────────────────────────────────────────

    [Fact]
    public void Private_products_allow_invite_only_and_nothing_else()
    {
        var allowed = AllowedFor(EventProduct.Private, AllPolicies);   // even asking for everything
        Assert.Equal([EventRegistrationPolicy.InviteOnly], allowed);
    }

    [Fact]
    public void A_type_allow_list_can_narrow_the_product_but_never_widen_it()
    {
        // The taxonomy says this type permits Open; the product says Private. Product wins.
        var allowed = AllowedFor(EventProduct.Private, [EventRegistrationPolicy.Open]);
        Assert.Equal([EventRegistrationPolicy.InviteOnly], allowed);

        // On a Public product the same list narrows as written.
        Assert.Equal([EventRegistrationPolicy.Open],
            AllowedFor(EventProduct.Public, [EventRegistrationPolicy.Open]));
    }

    [Fact]
    public void A_type_with_no_allow_list_yet_still_obeys_the_product()
    {
        Assert.Equal(AllPolicies.Length, AllowedFor(EventProduct.Public, null).Count);
        Assert.Single(AllowedFor(EventProduct.Private, null));
    }

    // ── Step 4 · a policy outside the allow-list is rejected, not silently accepted ───────────

    [Fact]
    public void Selecting_a_policy_the_type_forbids_is_a_violation()
    {
        // Blood Drive: Open only.
        var r = Resolve(EventProduct.Public, [EventRegistrationPolicy.Open],
            EventRegistrationPolicy.InviteOnly, EventVisibility.Listed);

        Assert.Contains("registration_policy_not_allowed_for_type", r.Violations);
        Assert.NotEmpty(PublishBlockers(r));
    }

    [Fact]
    public void An_allowed_policy_produces_no_violations()
    {
        var r = Resolve(EventProduct.Public,
            [EventRegistrationPolicy.CollegeRestricted, EventRegistrationPolicy.InviteOnly],
            EventRegistrationPolicy.CollegeRestricted, EventVisibility.Listed);

        Assert.Empty(r.Violations);
        Assert.Contains("eligibility_criteria_required", r.Requirements);
    }

    // ── Step 5 · visibility ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_private_product_can_never_be_listed()
    {
        var r = Resolve(EventProduct.Private, null, EventRegistrationPolicy.InviteOnly, EventVisibility.Listed);
        Assert.Contains("private_product_cannot_be_listed", r.Violations);
    }

    [Fact]
    public void A_private_product_can_never_take_payment()
    {
        var r = Resolve(EventProduct.Private, null, EventRegistrationPolicy.InviteOnly,
            EventVisibility.Unlisted, isPaid: true);
        Assert.Contains("private_product_cannot_take_payment", r.Violations);
    }

    // ── Step 6 · D9 invitation rules ────────────────────────────────────────────────────────

    [Fact]
    public void Being_invited_does_not_bypass_payment()
    {
        var r = Resolve(EventProduct.Public, null, EventRegistrationPolicy.InviteOnly,
            EventVisibility.Unlisted, isPaid: true);

        Assert.Contains("invitation_list_required", r.Requirements);
        Assert.Contains("invited_registrants_still_pay", r.Requirements);
        Assert.Empty(r.Violations);   // a paid invite-only PUBLIC event is legitimate
    }

    // ── Step 7 · gates are derived, never declared ──────────────────────────────────────────

    [Fact]
    public void Every_policy_derives_exactly_one_gate_set()
    {
        foreach (var p in AllPolicies)
        {
            var r = Resolve(EventProduct.Public, null, p, EventVisibility.Listed);
            Assert.NotEmpty(r.Gates);
        }
    }

    [Theory]
    [InlineData(EventRegistrationPolicy.Open, RegistrationGate.Open)]
    [InlineData(EventRegistrationPolicy.InviteOnly, RegistrationGate.Invite)]
    [InlineData(EventRegistrationPolicy.ApprovalRequired, RegistrationGate.Approval)]
    [InlineData(EventRegistrationPolicy.CollegeRestricted, RegistrationGate.Prerequisite)]
    public void Gates_follow_the_selected_policy(EventRegistrationPolicy policy, RegistrationGate gate)
    {
        var r = Resolve(EventProduct.Public, null, policy, EventVisibility.Listed);
        Assert.Contains(gate, r.Gates);
    }

    [Fact]
    public void VerifiedOnly_is_an_identity_bar_not_a_queue()
    {
        var r = Resolve(EventProduct.Public, null, EventRegistrationPolicy.VerifiedOnly, EventVisibility.Listed);
        Assert.Equal(IdentityRequirement.Verified, r.Identity);
        Assert.DoesNotContain(RegistrationGate.Approval, r.Gates);   // verified ≠ waiting for a human
    }

    // ── Checklists are projections of one model ─────────────────────────────────────────────

    [Fact]
    public void Publish_and_reviewer_checklists_project_the_same_resolution()
    {
        var r = Resolve(EventProduct.Private, null, EventRegistrationPolicy.InviteOnly,
            EventVisibility.Listed, isPaid: true);

        // Every publish blocker must appear in the reviewer's list — they cannot disagree about whether
        // the event is publishable, because both read the same array.
        Assert.All(PublishBlockers(r), b => Assert.Contains(b, ReviewerChecklist(r)));
    }

    [Fact]
    public void Resolution_is_deterministic()
    {
        var a = Resolve(EventProduct.Public, null, EventRegistrationPolicy.ApprovalRequired, EventVisibility.Listed);
        var b = Resolve(EventProduct.Public, null, EventRegistrationPolicy.ApprovalRequired, EventVisibility.Listed);
        Assert.Equal(a.Gates, b.Gates);
        Assert.Equal(a.Requirements, b.Requirements);
        Assert.Equal(a.Violations, b.Violations);
    }
}
