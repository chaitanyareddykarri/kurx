import { NextResponse } from "next/server";
import { currentSession } from "@/lib/session";
import { siteConfig } from "@/lib/site";

/// Forwards a participant list to `POST /v1/events/{id}/certificate-participants/preview` (D-344, Phase 6).
///
/// A route handler rather than a Server Action, deliberately. Actions were tried first and failed in a way
/// that produced **no entry in either server log** — the call never reached application code, so nothing
/// could report why. Three properties of actions explain that and none of them apply here:
///
///   * a File argument has to survive the action's own serialisation, which is an extra encoding step
///     between the browser and any code we wrote;
///   * actions carry a per-build id, so a tab open across a deploy calls an id the server no longer has;
///   * the action body limit is separate from — and was smaller than — the limit the endpoint enforces.
///
/// A plain multipart POST has none of that. It is also the shape already used by `/api/ticket-qr` and
/// `/api/events/[id]/certificates/export`, and unlike an action it can be exercised with curl, which is
/// what made this path verifiable at all.
///
/// Authorization is **not** re-implemented: the API owns it and its answer, including the status, is
/// passed through verbatim.
export async function POST(request: Request, { params }: { params: { id: string } }) {
  const session = await currentSession();
  if (!session) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  // Buffered rather than streamed: the endpoint caps uploads at 10MB, so there is nothing to gain from a
  // streaming body beyond the `duplex` footgun it brings with it.
  const body = await request.arrayBuffer();

  const upstream = await fetch(
    `${siteConfig.apiBaseUrl}/v1/events/${params.id}/certificate-participants/preview`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        // Forwarded intact — it carries the multipart boundary, without which the body is unparseable.
        "Content-Type": request.headers.get("content-type") ?? "application/octet-stream",
      },
      body,
      cache: "no-store",
    });

  const text = await upstream.text();
  try {
    return NextResponse.json(JSON.parse(text), { status: upstream.status });
  } catch {
    // A non-JSON upstream response is still an answer; turning it into a 500 here would hide it.
    return NextResponse.json(
      { error: "upstream_unreadable", detail: text.slice(0, 500) }, { status: upstream.status });
  }
}
