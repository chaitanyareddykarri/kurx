import { NextResponse } from "next/server";
import { currentSession } from "@/lib/session";

/// Hands the session's access token to same-origin client JS **for the SignalR connection only**
/// (D-109).
///
/// Why this exists: a browser WebSocket cannot send an Authorization header, so `/hubs/chat`
/// — which is `[Authorize]` — can only be reached with `?access_token=`. The session token lives in
/// an httpOnly cookie precisely so page scripts cannot read it, and that cookie cannot travel on a
/// WebSocket handshake the way it does on a fetch.
///
/// The tradeoff was taken deliberately and is worth stating plainly: this narrows the httpOnly
/// protection, because script that runs on the page can now obtain the access token. It does not
/// widen what an attacker could *do* — the same script could already drive every chat server action
/// as the user — but it does make the token itself exfiltratable, so it survives beyond the tab.
///
/// Two things keep the blast radius small:
///   * the refresh token is never returned, so a stolen access token dies at its own expiry rather
///     than being renewable;
///   * `no-store` keeps it out of every cache, including the PWA service worker.
///
/// REST is deliberately NOT routed through this token — chat reads and writes stay in server
/// actions (`lib/chat-actions.ts`), so the token's only job in the browser is opening the socket.
export async function GET() {
  const session = await currentSession();
  if (!session) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  return NextResponse.json(
    { accessToken: session.accessToken },
    { headers: { "Cache-Control": "no-store, no-cache, must-revalidate", Pragma: "no-cache" } }
  );
}
