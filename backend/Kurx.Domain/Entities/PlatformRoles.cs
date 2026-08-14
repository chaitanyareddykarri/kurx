using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>A platform-wide role granted to a user (SuperAdmin / VerificationReviewer / FinanceOps /
/// Support / ReadOnlyAuditor). Read LIVE per request (M2, D-040) — platform authority is never baked
/// into a JWT, so a grant/revoke takes effect on the very next request. Distinct from org-scoped
/// membership roles (Owner/Manager/Staff/Finance), which stay in <c>memberships</c>.</summary>
public class PlatformRoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public PlatformRole Role { get; set; }
    public Guid? GrantedBy { get; set; }                // null = system (e.g. IsKurxAdmin backfill)
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }            // null = no expiry; past = inactive
}
