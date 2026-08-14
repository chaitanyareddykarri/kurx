import { headers } from "next/headers";
import { requireStaffSession } from "@/lib/session";
import { AppShell } from "@/components/layout/app-shell";
import { RoleRequired } from "@/components/layout/role-required";
import { requiredRolesFor } from "@/components/layout/nav-config";
import { hasRole } from "@/lib/roles";
import { getDashboardSummary, type DashboardSummary } from "@/lib/api";

// Gate for the whole console: signed in + holds a platform role, else redirected
// (to /login or /forbidden) by requireStaffSession.
export default async function ConsoleLayout({ children }: { children: React.ReactNode }) {
  const session = await requireStaffSession();

  /*
   * …and then the gate for THIS route.
   *
   * `requireStaffSession()` asks only whether the caller holds *some* platform role, so until now the
   * console's role-based information architecture was declared in `NAV` and enforced nowhere: a
   * Support admin who typed `/staff` reached it, and a Reviewer who typed `/certificates` reached a
   * screen whose every call the backend then refuses. Nothing leaked — the backend is the authority —
   * but hiding a destination and then serving it is precisely the contradiction the nav's own
   * per-item comments were written to avoid.
   *
   * The required roles come from `NAV` itself, so there is one source of truth for what an operator
   * can see and what they can reach. The pathname arrives on a header the middleware sets, because a
   * server layout cannot read it and moving the whole console shell to the client to get
   * `usePathname()` would be a much larger change for a smaller result.
   */
  const pathname = headers().get("x-kurx-pathname") ?? "/";
  const required = requiredRolesFor(pathname);
  const permitted = hasRole(session.roles, required);

  // Best-effort: powers the sidebar's queue-count badges. A failure here should never block the
  // whole console shell from rendering — the badges just don't show.
  let summary: DashboardSummary | null = null;
  try {
    summary = await getDashboardSummary(session.accessToken);
  } catch {
    summary = null;
  }

  return (
    <AppShell roles={session.roles} name={session.me.name} summary={summary}>
      {/* The shell stays, so the operator can navigate somewhere they *can* use rather than
          landing on a dead end. */}
      {permitted ? children : <RoleRequired required={required} held={session.roles} />}
    </AppShell>
  );
}
