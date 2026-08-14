import type { Me } from "@/lib/api";

// The five platform roles (backend PlatformRole enum, D-040). SuperAdmin implies all.
export const PLATFORM_ROLES = [
  "SuperAdmin",
  "VerificationReviewer",
  "FinanceOps",
  "Support",
  "ReadOnlyAuditor"
] as const;
export type PlatformRole = (typeof PLATFORM_ROLES)[number];

export const ROLE_LABELS: Record<PlatformRole, string> = {
  SuperAdmin: "Super Admin",
  VerificationReviewer: "Trust & Safety",
  FinanceOps: "Finance",
  Support: "Support",
  ReadOnlyAuditor: "Auditor"
};

/**
 * Derive the signed-in user's platform roles from /v1/me.
 *
 * Backend gap: /v1/me currently returns only `is_platform_reviewer`, so today the
 * only role we can resolve is VerificationReviewer. Full RBAC needs the backend to
 * return `platform_roles: string[]` (documented as a Phase-2 requirement). When that
 * lands, `me.platform_roles` below becomes authoritative and the boolean fallback drops.
 *
 * Authority is never faked here in any environment: roles come from real platform_roles grants,
 * and the first SuperAdmin is bootstrapped server-side from configuration (D-274).
 */
export function deriveRoles(me: Me): PlatformRole[] {
  if (me.platform_roles && me.platform_roles.length > 0) {
    return me.platform_roles.filter((r): r is PlatformRole =>
      (PLATFORM_ROLES as readonly string[]).includes(r)
    );
  }
  return me.is_platform_reviewer ? ["VerificationReviewer"] : [];
}

/** SuperAdmin passes every gate; otherwise the user must hold one of `allowed`. */
export function hasRole(roles: PlatformRole[], allowed: readonly PlatformRole[]): boolean {
  if (roles.includes("SuperAdmin")) return true;
  if (allowed.length === 0) return true; // no restriction
  return roles.some((r) => allowed.includes(r));
}

/** A user is Kurx staff (may enter the console) if they hold any platform role. */
export function isStaff(roles: PlatformRole[]): boolean {
  return roles.length > 0;
}
