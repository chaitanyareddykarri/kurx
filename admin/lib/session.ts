import "server-only";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { getMe, type Me } from "@/lib/api";
import { deriveRoles, isStaff, type PlatformRole } from "@/lib/roles";
// The console's OWN cookies, not web's. See auth-cookies.ts for why they must differ.
import { ACCESS_COOKIE, REFRESH_COOKIE } from "@/lib/auth-cookies";

const cookieOpts = {
  httpOnly: true,
  sameSite: "lax" as const,
  secure: process.env.NODE_ENV === "production",
  path: "/"
};

export async function saveSession(accessToken: string, refreshTokenValue: string) {
  cookies().set(ACCESS_COOKIE, accessToken, cookieOpts);
  cookies().set(REFRESH_COOKIE, refreshTokenValue, cookieOpts);
}

export async function clearSession() {
  cookies().delete(ACCESS_COOKIE);
  cookies().delete(REFRESH_COOKIE);
}

export type AdminSession = { accessToken: string; refreshToken: string; me: Me; roles: PlatformRole[] };

/** Resolve the current session (refreshing a stale access token once). Null if signed out. */
export async function currentSession(): Promise<AdminSession | null> {
  const accessToken = cookies().get(ACCESS_COOKIE)?.value;
  const refreshTokenValue = cookies().get(REFRESH_COOKIE)?.value;
  if (!accessToken || !refreshTokenValue) return null;

  try {
    const me = await getMe(accessToken);
    return { accessToken, refreshToken: refreshTokenValue, me, roles: deriveRoles(me) };
  } catch {
    // Token refresh runs in middleware.ts before the render, where cookies are mutable. Never write
    // cookies here: Next forbids it during a Server Component render ("Cookies can only be modified in
    // a Server Action or Route Handler"). A failure now just means "not authenticated this request".
    return null;
  }
}

/** Gate for every console route: must be signed in AND hold a platform role. */
export async function requireStaffSession(): Promise<AdminSession> {
  const session = await currentSession();
  if (!session) redirect("/login");
  if (!isStaff(session.roles)) redirect("/forbidden");
  return session;
}
