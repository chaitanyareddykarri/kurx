using Kurx.Domain.Enums;

namespace Kurx.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
}

/// <summary>Event ↔ Tag join (many-to-many).</summary>
public class EventTag
{
    public Guid EventId { get; set; }
    public Guid TagId { get; set; }
}

public class Venue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? GoogleMapsUrl { get; set; }
    public int? Capacity { get; set; }
    public bool HasParking { get; set; }
    public bool IsAccessible { get; set; }
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

public class VenueImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; }
    public string Key { get; set; } = null!;
    public int Sort { get; set; }
}

/// <summary>Reusable event templates (system-seeded, e.g. Conference/Workshop, or org-custom).
/// DefaultSectionsJson describes which optional sections (schedule/speakers/sponsors/...) a new
/// event from this template starts with — purely a UI/organizer-dashboard hint, never enforced server-side.</summary>
/// <summary>An event Template (V3 §13.1, activated in Phase 14→15). Scoped (PLATFORM|ORG|UNIT|PERSONAL, most-specific
/// wins) and <b>versioned</b> — versions of one template share a <see cref="RootTemplateId"/>; a published version is
/// immutable, so an event that recorded <c>created_from_template_version</c> is unaffected by later edits (§3.5/§13.1).
/// It is <b>declarative configuration only</b> (D-132): it carries a capability preset + config, branding, default
/// audience, timezone/metadata, and form-field/agenda/stage/participant-role declarations — and <b>never</b> dates,
/// slug, status, inventory, pools, or anything financial. It only references capabilities that exist in the closed
/// registry (§11). The prior inert fields (Name/Slug/Description/OrgId/IsSystem/DefaultSectionsJson) are retained.</summary>
public class EventTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrgId { get; set; }                    // null = system/platform template, shared by everyone
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Description { get; set; } = "";
    public string DefaultSectionsJson { get; set; } = "[]";   // legacy (inert); kept for backward compatibility
    public bool IsSystem { get; set; }
    // V3 §13.1 activation (Phase 15) — scope, ownership, versioning, and the declarative config.
    public TemplateScope Scope { get; set; } = TemplateScope.Org;
    public Guid? OrgUnitId { get; set; }                // set for UNIT scope
    public Guid? OwnerUserId { get; set; }              // set for PERSONAL scope
    public string? KindSlug { get; set; }              // the Kind this template targets (§13.1)
    public int Version { get; set; } = 1;
    public TemplateState State { get; set; } = TemplateState.Draft;
    public Guid RootTemplateId { get; set; }            // stable identity across versions (= the v1 id); events reference this
    public string ConfigJson { get; set; } = "{}";      // jsonb — the declarative preset/config (capabilities, branding, …)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

public class Speaker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string Name { get; set; } = null!;
    public string Bio { get; set; } = "";
    public string? PhotoKey { get; set; }
    public string Company { get; set; } = "";
    public string Role { get; set; } = "";
    public string? SocialLinksJson { get; set; }
    /// <summary>Optional link to a real Kurx account, set by the organizer when the speaker happens to
    /// be a registered user (D-20x). Null is the common case — most speakers are curated content
    /// (name/bio/photo) with no account at all, and "Connect" correctly does not apply to them.</summary>
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

/// <summary>Speaker profiles associated with an event (not every speaker has a specific session).</summary>
public class EventSpeaker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid SpeakerId { get; set; }
    public int Sort { get; set; }
}

public class Sponsor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string Name { get; set; } = null!;
    public string? LogoKey { get; set; }
    public string Website { get; set; } = "";
    public SponsorTier Tier { get; set; } = SponsorTier.Partner;
    public int Priority { get; set; }
    /// <summary>Booth/stall identifier at a physical event (D-265), e.g. "B-12". Free text — venues
    /// number stalls however they like, and an enum here would fit exactly one venue.</summary>
    public string? Booth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

public class EventSponsor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid SponsorId { get; set; }
    public int Sort { get; set; }
}

/// <summary>One agenda item — a talk or a break — within an event's (possibly multi-day) schedule.</summary>
/// <summary>An <b>AgendaItem</b> (V3 §3.1 CONTAINMENT, Phase 12) — a scheduled item that lives inside one Event with
/// no participant set of its own. Per §3.4 it has <b>no Pass, Registration, or Credential</b> (attendance is derived
/// from the parent Credential plus a scan), but it <b>may hold an <see cref="InventoryPool"/></b> for a seat limit —
/// the boundary is commerce and identity, not capacity. The pool link is configuration this phase; enforcing the seat
/// limit at scan is a later attendance/gate concern. (Historical type name <c>EventSession</c> retained for backward
/// compatibility — the existing schedule surface is unchanged.)</summary>
public class EventSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = "";
    public ScheduleItemKind Kind { get; set; } = ScheduleItemKind.Session;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int Sort { get; set; }
    public Guid? InventoryPoolId { get; set; }          // §3.4 — an AgendaItem may hold a pool for seat limits (config-only this phase)
}

/// <summary>Speaker(s) assigned to a specific schedule session.</summary>
public class EventSessionSpeaker
{
    public Guid SessionId { get; set; }
    public Guid SpeakerId { get; set; }
}

/// <summary>Gallery images / documents / poster / brochure / rules-PDF for an event. The primary banner
/// is kept as Event.BannerKey for quick access; everything else (including additional banners) lives here.</summary>
public class EventMedia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public MediaKind Kind { get; set; }
    public string Key { get; set; } = null!;
    public string Caption { get; set; } = "";
    public int Sort { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
