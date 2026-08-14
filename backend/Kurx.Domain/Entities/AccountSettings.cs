using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

/// <summary>
/// One user's delivery choice for one notification category (D-263).
///
/// <para>Row-per-category rather than 48 boolean columns on <c>users</c>: categories are a product
/// vocabulary that will grow, and growing it should be a row, not a migration.</para>
///
/// <para><b>Absence of a row is not "off".</b> A user who has never opened the settings screen has no
/// rows at all and must keep receiving exactly what they receive today, so the default is every channel
/// on except WhatsApp. That is why there is no backfill: the default *is* the pre-existing behaviour.</para>
/// </summary>
public class NotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Unique with <see cref="UserId"/>.</summary>
    public NotificationCategory Category { get; set; }

    public bool InApp { get; set; } = true;
    public bool Push { get; set; } = true;
    public bool Email { get; set; } = true;

    /// <summary>Off by default. WhatsApp is the one channel that costs money per message and reaches a
    /// surface users treat as personal, so it is opt-in rather than opt-out.</summary>
    public bool WhatsApp { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A one-way block (D-263). Unique on the ordered pair (blocker, blocked), so blocking twice is a no-op
/// and A-blocks-B is a different row from B-blocks-A.
///
/// <para>Enforcement is symmetric even though the row is not: if <b>either</b> direction exists, neither
/// party sees the other's content and neither can open a DM. A block that only hid one direction would
/// let the blocked party keep reading and keep reaching out, which is not what anyone means by "block".</para>
/// </summary>
public class UserBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
