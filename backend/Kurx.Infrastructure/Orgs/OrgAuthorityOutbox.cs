using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Infrastructure.Persistence;

namespace Kurx.Infrastructure.Orgs;

/// <summary>The one place an organization authority change is staged for chat (D-304).
///
/// <para>Three services write membership rows — <see cref="OrgService"/> (add, role change, removal),
/// <see cref="OrgInvitationService"/> (acceptance) and <see cref="MembershipVerificationService"/>
/// (approval). All three need the identical consequence, so they share this rather than carrying three
/// copies of it: a second propagation path is precisely how the grant half of this went missing while the
/// revoke half (D-299) shipped.</para>
///
/// <para><b>Staged, never sent.</b> The row joins the caller's unit of work and commits with the membership
/// change, so the two can never disagree. An inline <c>chat.X(...)</c> after <c>SaveChanges</c> reaches the
/// Redis-backed broadcaster, and a transient there leaves the authority committed and the room's roster
/// stale forever with nothing to retry — the D-299 failure exactly.</para>
///
/// <para><b>Carries WHO, never the resulting role.</b> <c>ChatService.SyncOrgAuthorityAsync</c> re-resolves
/// live authority per event at dispatch time. That is what makes a redelivery a no-op, and what makes two
/// concurrent membership changes converge on committed state instead of on the order their rows were
/// written — a payload carrying <c>role</c> would have neither property.</para>
///
/// <para>The random key suffix follows <c>ParticipantService</c>, <c>SeatBlockService</c> and
/// <c>OrderService</c>: the handler is idempotent, so the key exists for the row's own uniqueness and never
/// needs to dedup.</para></summary>
public static class OrgAuthorityOutbox
{
    /// <summary>Call before <c>SaveChangesAsync</c>, on the transaction that changes the membership.
    ///
    /// <para>Deliberately not called from organization <i>creation</i>: the founder's Owner row predates any
    /// event, so there is no published room to converge and the message would be pure dispatch cost.</para></summary>
    public static void Stage(KurxDbContext db, Guid orgId, Guid userId) => db.OutboxMessages.Add(new OutboxMessage
    {
        Type = "org.authority_changed",
        PayloadJson = JsonSerializer.Serialize(new { orgId, userId }),
        IdempotencyKey = $"org_authority:{orgId}:{userId}:{Guid.NewGuid():N}",
    });
}
