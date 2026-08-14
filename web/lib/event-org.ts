import { notFound } from "next/navigation";
import { requireSession } from "@/lib/session";
import { getEvent } from "@/lib/api";
import { getWorkspaceCapabilities } from "@/lib/capabilities";

/// Resolves the organization an event REPRESENTS, from the event itself (D-267/D-273a). Representation
/// is not ownership — the owner is always the user in `created_by` (D-268).
///
/// This replaced `requireCurrentOrg()`, which read a `kurx_org` cookie set by an organization switcher.
/// That was the org-first model's last hold on event management: every `/host/events/[id]/*` page built
/// its API calls from whichever organization happened to be *selected*, not from the one the event
/// actually belongs to — so a person who represents two organizations got 404s on their own event
/// whenever the switcher pointed at the other one. The event knows its own org; nothing else needs to.
export async function requireEventOrg(eventId: string) {
  const session = await requireSession();
  // The API answers 404 for an event the caller may not manage — hidden resource, never 403 (D-018).
  // Surfacing it as Next's not-found keeps that boundary instead of leaking an error page.
  const event = await getEvent(session.accessToken, eventId).catch(() => notFound());
  // Cached per request, and the event workspace layout reads the same key — so pages that need the
  // caller's authority pay nothing extra for it.
  const caps = await getWorkspaceCapabilities(session.accessToken, event.representing_org_id);
  // `role` is the caller's authority over the event's representation — an additional grant on top of
  // ownership, never the source of it (D-268).
  return {
    session,
    event,
    representingOrgId: event.representing_org_id,
    // Kept so the 16 `/host/events/[id]/*` pages that thread this into org-scoped sub-resource routes
    // did not each need touching in a rename. Same value, older name.
    orgId: event.representing_org_id,
    caps,
    role: caps.representation.authority ?? "",
  };
}
