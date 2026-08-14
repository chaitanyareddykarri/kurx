import { NextResponse } from "next/server";
import { currentSession } from "@/lib/session";
import { siteConfig } from "@/lib/site";

/// Streams a ticket's real QR image from `GET /v1/tickets/{code}/qr.png`.
///
/// Why a route handler rather than an `<img src>` pointing straight at the API: that endpoint is
/// `RequireAuthorization()`, and a browser image request cannot carry an `Authorization` header. The
/// session's access token lives in an httpOnly cookie so page scripts cannot read it.
///
/// So the fetch happens here, on the server, and only the PNG bytes cross to the browser. Unlike
/// `/api/realtime-token` — which had to widen the httpOnly boundary because a WebSocket handshake
/// left no alternative — nothing is given up here: the token never leaves the server.
///
/// Authorization is **not** re-implemented. The backend owns it (the holder, or staff of the
/// representing org) and its answer is passed through verbatim, including the 403. A proxy that
/// decided for itself who may see a ticket would be a second, drifting copy of that rule.
export async function GET(_request: Request, { params }: { params: { code: string } }) {
  const session = await currentSession();
  if (!session) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  const upstream = await fetch(`${siteConfig.apiBaseUrl}/v1/tickets/${params.code}/qr.png`, {
    headers: { Authorization: `Bearer ${session.accessToken}` },
    cache: "no-store",
  });

  if (!upstream.ok) {
    return NextResponse.json({ error: "unavailable" }, { status: upstream.status });
  }

  return new NextResponse(upstream.body, {
    headers: {
      "Content-Type": "image/png",
      // A ticket QR is a credential. It must not sit in a shared cache, a CDN or the PWA's service
      // worker, where it would outlive the session that was entitled to it.
      "Cache-Control": "private, no-store",
    },
  });
}
