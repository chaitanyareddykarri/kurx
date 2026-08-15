import {
  Award,
  Bookmark,
  ClipboardCheck,
  Handshake,
  Home,
  LayoutDashboard,
  MailOpen,
  MessagesSquare,
  Newspaper,
  Settings,
  Ticket,
  Users,
  type LucideIcon
} from "lucide-react";


export type NavItem = { href: string; label: string; icon: LucideIcon; external?: boolean };

/**
 * The product flow's Main Application areas, in order. Kept identical to the
 * Flutter shell's tabs so the two surfaces teach the same map — **Home ·
 * Community · Posts · Messages · Workspace** — with Profile and Notifications as
 * fixed header corners rather than nav entries: they are places you visit and
 * come back from, not places you dwell.
 *
 * Extracted from `app-shell.tsx` in Phase 11 because the mobile bottom bar and
 * the desktop sidebar must render the same list; two copies would drift.
 * (`docs/ui-ux/information-architecture.md` §2.)
 */
export const primaryNav: NavItem[] = [
  { href: "/discover", label: "Home", icon: Home },
  { href: "/allies", label: "Community", icon: Handshake },
  { href: "/posts", label: "Posts", icon: Newspaper },
  { href: "/chats", label: "Messages", icon: MessagesSquare },
  { href: "/workspace", label: "Workspace", icon: LayoutDashboard }
];

/** Reached *through* one of the areas above, so ranked below the rule. */
export const secondaryNav: NavItem[] = [
  // D-266 M6 (D9 Method A) — the invitee's inbox. Sits beside My Tickets
  // deliberately: an invitation is adjacent to a ticket in a guest's mental model
  // but is emphatically not one, and the page says so.
  { href: "/invitations", label: "Invitations", icon: MailOpen },
  // D-319 — kept separate from Invitations on purpose. An invitation grants permission to *register*;
  // an assignment is a role on the event's team, carrying chat access and a public-profile credential.
  // The invitations page's own copy works hard not to read as a ticket, and folding a second meaning
  // into it would undo that.
  { href: "/assignments", label: "Assignments", icon: ClipboardCheck },
  { href: "/tickets", label: "My Tickets", icon: Ticket },
  { href: "/my-certificates", label: "My Certificates", icon: Award },
  { href: "/saved", label: "Saved", icon: Bookmark },
  { href: "/groups", label: "Groups", icon: Users },
  // "Create event" was here until D-305. Creation is entered from **Profile**, and a primary-nav entry
  // beside Saved and Groups is a second competing door — it also reads as an app-wide mode, which
  // hosting is not. Profile is the one door; Workspace lists what you already run.
  { href: "/settings", label: "Settings", icon: Settings }
];

/**
 * `reviewerNav` — a "Platform review" group linking to the admin console — was here until it was
 * deleted outright.
 *
 * **This app is the user account. It shows no staff or admin surface, to anybody.** One number can
 * legitimately be both a user and a SuperAdmin, and web kept revealing the second identity inside the
 * first: signing in here as a platform admin grew an extra nav section. Which surface you are on is
 * decided by which door you signed in through, not by what your account happens to hold — and the
 * console has its own sign-in at `siteConfig.adminUrl` for exactly that.
 *
 * `/v1/me` still returns `is_platform_reviewer`; web simply no longer renders anything from it.
 */
