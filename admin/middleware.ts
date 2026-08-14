import { NextResponse, type NextRequest } from "next/server";

// Public (unauthenticated) routes. Everything else requires a session cookie; the
// authoritative staff-role check happens server-side in requireStaffSession().
const PUBLIC_PATHS = ["/login", "/forbidden"];

// The console's own names, shared with lib/session.ts so a rename cannot leave the middleware
// rotating one cookie while the session reads another.
import { ACCESS_COOKIE, REFRESH_COOKIE } from "@/lib/auth-cookies";

// Server-side origin: middleware runs inside the container, so it uses the internal API_URL (as SSR
// does), falling back to the public URL then the local backend — same resolution as lib/site.ts.
const apiBase = process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

const authCookieOpts = {
  httpOnly: true,
  sameSite: "lax" as const,
  secure: process.env.NODE_ENV === "production",
  path: "/"
};

/** Optional network isolation: set ADMIN_IP_ALLOWLIST="1.2.3.4,5.6.7.8" to hard-block
 *  any request from outside the office/VPN range. Empty = disabled (rely on deploy net). */
function ipAllowed(req: NextRequest): boolean {
  const allow = process.env.ADMIN_IP_ALLOWLIST;
  if (!allow) return true;
  const list = allow.split(",").map((s) => s.trim()).filter(Boolean);
  const fwd = req.headers.get("x-forwarded-for")?.split(",")[0]?.trim() ?? "";
  return list.includes(fwd);
}

// True if the JWT is missing, malformed, or within 30s of expiry. Decoded locally (no network).
function accessExpired(token: string | undefined): boolean {
  if (!token) return true;
  const parts = token.split(".");
  if (parts.length !== 3) return true;
  try {
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = payload + "=".repeat((4 - (payload.length % 4)) % 4);
    const claims = JSON.parse(atob(padded)) as { exp?: number };
    return typeof claims.exp !== "number" || claims.exp * 1000 <= Date.now() + 30_000;
  } catch {
    return true;
  }
}

type Rotation = { access: string; refresh: string } | "clear";

// Same single-flight guard as web/middleware.ts, for the same reason and with higher stakes here:
// refresh rotation is strictly single-use server-side (D-240), so of two requests presenting one
// cookie the loser gets 401 — and this middleware answers that by clearing the cookies and bouncing
// the operator to /login mid-task. Keyed by the presented token (never one global promise, which
// would leak one operator's tokens to another) and retained briefly after settling, since a racing
// request captured the OLD cookie and can arrive after the winner already finished.
// ponytail: per-process map; admin is single-instance in docker-compose. Shared store only if scaled.
const REPLAY_MS = 10_000;
const recentRotations = new Map<string, { promise: Promise<Rotation>; expires: number }>();

async function requestRotation(refresh: string): Promise<Rotation> {
  try {
    const res = await fetch(`${apiBase}/v1/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: refresh })
    });
    if (!res.ok) throw new Error(`refresh ${res.status}`);
    const data = (await res.json()) as { access_token: string; refresh_token: string };
    return { access: data.access_token, refresh: data.refresh_token };
  } catch {
    return "clear";
  }
}

function rotateOnce(refresh: string): Promise<Rotation> {
  const now = Date.now();
  for (const [token, entry] of recentRotations) if (entry.expires <= now) recentRotations.delete(token);

  const inFlight = recentRotations.get(refresh);
  if (inFlight) return inFlight.promise;

  const promise = requestRotation(refresh);
  recentRotations.set(refresh, { promise, expires: now + REPLAY_MS });
  return promise;
}

// Rotate an expired access token here, where cookies are mutable — currentSession() must NOT do it
// during a render (Next throws "Cookies can only be modified in a Server Action or Route Handler").
// Mutates req.cookies so this request's render sees the result; returns the pair to persist, "clear"
// if the refresh token is dead, or null if nothing changed.
async function rotateTokens(req: NextRequest): Promise<Rotation | null> {
  const access = req.cookies.get(ACCESS_COOKIE)?.value;
  const refresh = req.cookies.get(REFRESH_COOKIE)?.value;
  if (!refresh || !accessExpired(access)) return null;

  const rotation = await rotateOnce(refresh);
  if (rotation === "clear") {
    req.cookies.delete(ACCESS_COOKIE);
    req.cookies.delete(REFRESH_COOKIE);
    return "clear";
  }

  req.cookies.set(ACCESS_COOKIE, rotation.access);
  req.cookies.set(REFRESH_COOKIE, rotation.refresh);
  return rotation;
}

export async function middleware(req: NextRequest) {
  if (!ipAllowed(req)) return new NextResponse("Forbidden", { status: 403 });

  const rotation = await rotateTokens(req);

  const { pathname } = req.nextUrl;
  const isPublic = PUBLIC_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`));
  const hasSession = Boolean(req.cookies.get(ACCESS_COOKIE)?.value); // reflects the rotation above

  if (!isPublic && !hasSession) {
    const url = req.nextUrl.clone();
    url.pathname = "/login";
    if (pathname !== "/") url.searchParams.set("next", pathname);
    return NextResponse.redirect(url);
  }

  // A server layout cannot read the current path, and the console gate needs it to work out which
  // roles the route requires (Phase 26). Forwarding it here keeps that check on the server rather
  // than moving the whole console shell to the client to get `usePathname()`.
  const forwarded = new Headers(req.headers);
  forwarded.set("x-kurx-pathname", pathname);

  const response = NextResponse.next({ request: { headers: forwarded } });
  if (rotation === "clear") {
    response.cookies.delete(ACCESS_COOKIE);
    response.cookies.delete(REFRESH_COOKIE);
  } else if (rotation) {
    response.cookies.set(ACCESS_COOKIE, rotation.access, authCookieOpts);
    response.cookies.set(REFRESH_COOKIE, rotation.refresh, authCookieOpts);
  }
  return response;
}

export const config = {
  // Skip Next internals and any file with an extension (static assets).
  matcher: ["/((?!_next|.*\\..*).*)"]
};
