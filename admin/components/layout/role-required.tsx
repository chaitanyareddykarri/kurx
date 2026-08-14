import { ShieldAlert } from "lucide-react";
import { ROLE_LABELS, type PlatformRole } from "@/lib/roles";

/**
 * Shown in place of a console screen the operator's roles do not cover.
 *
 * Distinct from `/forbidden`, which answers "this account is not Kurx staff at all" and signs you
 * out. This one answers "you are staff, but not for this" — so it keeps the shell and says which
 * role would be needed, because an operator who cannot tell the difference will file a bug about a
 * broken page.
 *
 * It names the requirement rather than pretending the page is absent. D-018's 404-not-403 rule
 * exists to stop *resource existence* leaking to outsiders; every reader here is already staff, and
 * the navigation openly lists these areas, so there is nothing to conceal and a great deal to
 * explain.
 */
export function RoleRequired({ required, held }: { required: PlatformRole[]; held: PlatformRole[] }) {
  const need = required.map((r) => ROLE_LABELS[r]).join(" or ");
  const have = held.length > 0 ? held.map((r) => ROLE_LABELS[r]).join(", ") : "no platform role";

  return (
    <div className="mx-auto max-w-md rounded-lg border border-border bg-surface p-6 text-center">
      <ShieldAlert className="mx-auto text-warning" size={36} aria-hidden />
      <h1 className="mt-3 text-lg font-semibold text-text">This area needs a different role</h1>
      <p className="mt-2 text-sm text-muted">
        It is open to <strong className="text-text">{need}</strong>. You hold{" "}
        <strong className="text-text">{have}</strong>.
      </p>
      <p className="mt-3 text-sm text-muted">
        Ask a Super Admin if you need access. Everything your roles do cover is still in the sidebar.
      </p>
    </div>
  );
}
