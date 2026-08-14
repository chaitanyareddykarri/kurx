import { NextResponse, type NextRequest } from "next/server";
import { routing } from "./i18n/routing";

// Two jobs, one pass:
//  1) Token refresh. `cookies().set()` throws during a Server Component render, so refreshing inside
//     currentSession() crashed every authed page once the access token expired. Middleware can mutate
//     cookies, so we rotate the token here and pages only ever READ the session.
//  2) Locale detection (unchanged) — cookie/Accept-Language, no URL rewrites.
const ACCESS_COOKIE = "kurx_access";
const REFRESH_COOKIE = "kurx_refresh";

// Server-side origin: middleware runs inside the container, so it uses the internal service URL
// (like SSR), not the browser-facing host URL.
const apiBase =
  process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

const authCookieOpts = {
  httpOnly: true,
  sameSite: "lax" as const,
  secure: process.env.NODE_ENV === "production",
  path: "/"
};

// Detect locale from cookie or Accept-Language — no URL rewrites.
// Pages live at app/(app)/... and app/(public)/... without a [locale] segment,
// so nextIntlMiddleware must NOT run (it rewrites to /en/... which 404s).
function detectLocale(request: NextRequest): string {
  const cookie = request.cookies.get("NEXT_LOCALE")?.value;
  if (cookie && (routing.locales as readonly string[]).includes(cookie)) return cookie;
  const lang = request.headers.get("Accept-Language")?.split(",")[0]?.split("-")[0];
  if (lang && (routing.locales as readonly string[]).includes(lang)) return lang;
  return routing.defaultLocale;
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

// Refresh-token rotation is strictly single-use server-side (D-240): of two requests presenting the
// same token, exactly one wins and the loser gets 401. Middleware runs per request — including every
// <Link> prefetch — so a page with several prefetched links reliably fires a burst of refreshes off
// one cookie. Without this map the losers would each return "clear", delete both cookies, and sign
// the user out mid-navigation.
//
// Keyed by the presented token, NEVER a single global promise: a shared one would hand one user's
// freshly minted tokens to another. Results are retained briefly after settling, not just while
// in flight, because the racing requests were captured with the OLD cookie and can arrive after the
// winner has already finished — they must still be answered with the winner's pair.
//
// ponytail: per-process map, so a second web instance still 401s a straggler; move to a shared store
// only if web is actually scaled out (it is single-instance in docker-compose today).
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

// Rotate an expired access token. Mutates request.cookies so THIS request's render sees the new
// token; returns the pair to persist on the response, "clear" if the refresh token is dead, or null
// if nothing changed.
async function rotateTokens(request: NextRequest): Promise<Rotation | null> {
  const access = request.cookies.get(ACCESS_COOKIE)?.value;
  const refresh = request.cookies.get(REFRESH_COOKIE)?.value;
  if (!refresh || !accessExpired(access)) return null;

  const rotation = await rotateOnce(refresh);
  if (rotation === "clear") return "clear";

  request.cookies.set(ACCESS_COOKIE, rotation.access);
  request.cookies.set(REFRESH_COOKIE, rotation.refresh);
  return rotation;
}

export default async function middleware(request: NextRequest) {
  const rotation = await rotateTokens(request);

  const locale = detectLocale(request);
  const response = NextResponse.next({ request: { headers: request.headers } });
  response.cookies.set("NEXT_LOCALE", locale, { maxAge: 60 * 60 * 24 * 365, httpOnly: false });
  response.headers.set("x-next-intl-locale", locale);

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
  matcher: ["/((?!_next|api|.*\\..*).*)"]
};
