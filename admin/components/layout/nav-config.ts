import {
  LayoutDashboard,
  ShieldCheck,
  CalendarCheck,
  Gavel,
  Flag,
  Ban,
  AlertTriangle,
  Users,
  Building2,
  CalendarDays,
  Award,
  Trophy,
  Mic,
  Handshake,
  Layers,
  KeyRound,
  ScrollText,
  Megaphone,
  HeartPulse,
  Activity,
  type LucideIcon
} from "lucide-react";
import type { PlatformRole } from "@/lib/roles";

export type NavItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  /** Roles allowed to see the item ([] = any staff). SuperAdmin always passes. */
  roles: PlatformRole[];
  /** false = designed but not built yet (Phase 2+) — shown disabled, never linked to a placeholder. */
  ready?: boolean;
};

export type NavGroup = { label: string; items: NavItem[] };

/*
 * The role-based information architecture. Every item here is built and reachable.
 *
 * **Phase 26 removed the five that were not.** Payments, Payout Approvals, Refunds, Templates and
 * Background Jobs had rendered as disabled `<span>`s since Phase 1 — `text-muted/50`, well under any
 * contrast floor, a permanent "soon" badge, and `title="Coming in a later phase"`, which is a note
 * between engineers shown to operators through a tooltip no keyboard or touch user can reach.
 *
 * Showing the map once is defensible; as a permanent state it is noise, and `docs/ui-ux/
 * information-architecture.md` §9 asked this phase to decide per item. None of the five can ship —
 * they have no routes, and Templates is backend-blocked (`GET /v1/templates` lists only Published,
 * so drafts are unlistable and the screen cannot exist until an admin inventory endpoint does). So
 * they are removed rather than left as doors that do not open. The intended map is preserved in
 * §9 of that document, which is where a plan belongs.
 */
export const NAV: NavGroup[] = [
  {
    label: "",
    items: [{ label: "Dashboard", href: "/", icon: LayoutDashboard, roles: [], ready: true }]
  },
  {
    label: "Trust & Safety",
    items: [
      { label: "Verification Queue", href: "/verification", icon: ShieldCheck, roles: ["VerificationReviewer"], ready: true },
      { label: "Event Approval", href: "/events/pending", icon: CalendarCheck, roles: ["VerificationReviewer"], ready: true },
      // The full review flow — claim, notes, reason codes. It existed and worked, and nothing
      // linked to it: not the nav, not the workspace sheet that names it in a comment. Phase 50.
      { label: "Event Review", href: "/events/review", icon: Gavel, roles: ["VerificationReviewer"], ready: true },
      { label: "Reports", href: "/reports", icon: Flag, roles: ["VerificationReviewer", "Support"], ready: true },
      { label: "Blacklist", href: "/blacklist", icon: Ban, roles: ["VerificationReviewer"], ready: true },
      { label: "Risk Flags", href: "/risk", icon: AlertTriangle, roles: ["VerificationReviewer", "FinanceOps"], ready: true }
    ]
  },
  {
    label: "People & Orgs",
    items: [
      { label: "Users", href: "/users", icon: Users, roles: ["Support", "VerificationReviewer"], ready: true },
      { label: "Organizations", href: "/organizations", icon: Building2, roles: ["Support", "VerificationReviewer", "FinanceOps"], ready: true }
    ]
  },
  {
    label: "Events",
    items: [
      // D-186: the tabbed Event Management workspace — Upcoming/Live/Completed/Draft/Under Review/
      // Cancelled/Archived/All in one screen. "Event Approval" above still stands alone (unchanged,
      // still works) for the focused pending-only queue; this is the full operational surface.
      { label: "Event Management", href: "/events", icon: CalendarDays, roles: ["VerificationReviewer"], ready: true },
      // SuperAdmin, not Reviewer: the certificate endpoints bypass the org-role check on the
      // `kurx_admin` claim, and PlatformRoleClaimsTransformation grants that to SuperAdmin only.
      // A Reviewer would see the screen and get a 403 they cannot resolve.
      { label: "Certificates", href: "/certificates", icon: Award, roles: ["SuperAdmin"], ready: true },
      // SuperAdmin for the same reason as Certificates: the competition gate is
      // IsOrganiserAsync = isAdmin || event:manage, and only SuperAdmin carries the kurx_admin claim.
      { label: "Competitions", href: "/competitions", icon: Trophy, roles: ["SuperAdmin"], ready: true },
      // Org-scoped routes reached via the event picker; CanManage short-circuits on kurx_admin.
      { label: "Speakers", href: "/speakers", icon: Mic, roles: ["SuperAdmin"], ready: true },
      { label: "Sponsors", href: "/sponsors", icon: Handshake, roles: ["SuperAdmin"], ready: true }
    ]
  },
  {
    // D-188 (Platform Taxonomy Management): the admin-manageable Audience/Category/Type workspace —
    // replaces the old flat "Categories" screen (parity verified: search, create/rename/reorder/
    // visibility/delete all carried over, plus lifecycle, metadata, capabilities, audit, import/export).
    // The old screen and its "Catalog" nav entry are retired in this same change.
    label: "Platform",
    items: [
      { label: "Event Taxonomy", href: "/platform/event-taxonomy", icon: Layers, roles: ["SuperAdmin"], ready: true }
    ]
  },
  {
    label: "System",
    items: [
      { label: "Staff & Roles", href: "/staff", icon: KeyRound, roles: ["SuperAdmin"], ready: true },
      { label: "Audit Log", href: "/audit", icon: ScrollText, roles: ["SuperAdmin", "ReadOnlyAuditor"], ready: true },
      { label: "Analytics", href: "/analytics", icon: Activity, roles: [], ready: true },
      { label: "Broadcast", href: "/broadcast", icon: Megaphone, roles: ["SuperAdmin"], ready: true },
      { label: "Health", href: "/health", icon: HeartPulse, roles: ["SuperAdmin"], ready: true }
    ]
  }
];

/**
 * The roles a route requires, resolved from the navigation itself.
 *
 * **Phase 26 found the console's information architecture declared here and enforced nowhere.**
 * `NAV` gates each destination by role — with careful reasoning per item, e.g. Certificates is
 * SuperAdmin because those endpoints check the `kurx_admin` claim and "a Reviewer would see the
 * screen and get a 403 they cannot resolve" — but every console page guards with
 * `requireStaffSession()` alone, which only asks whether the caller holds *some* platform role.
 *
 * So a Support admin who typed `/staff` reached it. Nothing leaked: the backend is the authority and
 * refuses the calls. But the console hid a destination and then served it, which is the contradiction
 * the nav's own comments were written to avoid.
 *
 * Longest-href match wins, the same rule the breadcrumb resolver uses, so `/users/[id]` inherits
 * `/users`. A route absent from `NAV` (`/account`, `/security`) requires no particular role — those
 * are every operator's own pages.
 */
export function requiredRolesFor(pathname: string): PlatformRole[] {
  let best: { href: string; roles: PlatformRole[] } | null = null;
  for (const group of NAV) {
    for (const item of group.items) {
      const matches =
        item.href === "/" ? pathname === "/" : pathname === item.href || pathname.startsWith(`${item.href}/`);
      if (matches && (!best || item.href.length > best.href.length)) {
        best = { href: item.href, roles: item.roles };
      }
    }
  }
  return best?.roles ?? [];
}
