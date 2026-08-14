import { NextResponse } from "next/server";
import { requireStaffSession } from "@/lib/session";

/// Same rationale as web's identical route (D-109): a browser WebSocket can't send an Authorization
/// header, so the SignalR hubs (`[Authorize]`) need `?access_token=`, which only same-origin script
/// can supply — hence handing the httpOnly-cookie-held access token to page JS for this one purpose.
/// REST stays on server actions; the token's only job in the browser is opening the socket.
/// `requireStaffSession`, not `currentSession`: every other route on this origin requires a platform
/// role, and this one asked only for *a* session. It was not an escalation — the token returned is
/// always the caller's own, and they could obtain it from web's identical route regardless — but the
/// admin origin should hand nothing to a non-staff caller, and matching the console's own rule costs
/// nothing. Defence in depth, not a closed leak.
export async function GET() {
  const session = await requireStaffSession();

  return NextResponse.json(
    { accessToken: session.accessToken },
    { headers: { "Cache-Control": "no-store, no-cache, must-revalidate", Pragma: "no-cache" } }
  );
}
