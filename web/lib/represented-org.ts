import { notFound } from "next/navigation";
import { requireSession } from "@/lib/session";
import { listMyRepresentations } from "@/lib/api";

/// The organization a supporting surface (payouts, team, verification) is about, taken from the URL.
///
/// Organizations survive the D-267 migration for exactly the reasons the product keeps them —
/// representation, verification, permissions, payouts, collaboration — and those surfaces genuinely need
/// to name one. What is gone is the *ambient* organization: a `kurx_org` cookie that every page read, so
/// the app always had a "current organization" whether the page was about one or not. Naming it in the
/// path makes the dependency explicit and local, and keeps these pages off the event-hosting path
/// entirely.
export async function requireRepresentedOrg(orgId: string) {
  const session = await requireSession();
  const representations = await listMyRepresentations(session.accessToken).catch(() => []);
  // Not a representative → 404, never 403: the same hidden-resource boundary the API keeps (D-018).
  const org = representations.find((r) => r.organization_id === orgId) ?? notFound();
  return { session, org };
}
