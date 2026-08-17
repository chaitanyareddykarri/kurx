import { NextResponse } from "next/server";
import { currentSession } from "@/lib/session";
import { siteConfig } from "@/lib/site";

/// Streams the issued-certificate record from `GET /v1/events/{id}/certificates/export`.
///
/// A route handler rather than a link straight to the API, for the same reason as `/api/ticket-qr`: that
/// endpoint is `RequireAuthorization()`, a plain `<a download>` cannot carry an `Authorization` header,
/// and the access token lives in an httpOnly cookie that page scripts cannot read. The fetch happens
/// here, on the server; only the CSV bytes cross to the browser.
///
/// Authorization is **not** re-implemented. The backend owns it — the export carries participant names
/// and email addresses and needs ManageContent — and its answer is passed through verbatim, 403 included.
/// A proxy that decided for itself who may download a participant list would be a second, drifting copy
/// of that rule.
export async function GET(request: Request, { params }: { params: { id: string } }) {
  const session = await currentSession();
  if (!session) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  // Passed through so a single run can be exported rather than the whole event.
  const batchId = new URL(request.url).searchParams.get("batchId");
  const query = batchId ? `?batchId=${encodeURIComponent(batchId)}` : "";

  const upstream = await fetch(
    `${siteConfig.apiBaseUrl}/v1/events/${params.id}/certificates/export${query}`,
    { headers: { Authorization: `Bearer ${session.accessToken}` }, cache: "no-store" });

  if (!upstream.ok) {
    return NextResponse.json({ error: "unavailable" }, { status: upstream.status });
  }

  return new NextResponse(upstream.body, {
    headers: {
      "Content-Type": "text/csv; charset=utf-8",
      // The filename the API chose, so a batch export and a whole-event export do not both land in the
      // downloads folder as "export".
      "Content-Disposition": upstream.headers.get("content-disposition")
        ?? `attachment; filename="certificates.csv"`,
      // A list of participant names and email addresses. It must not sit in a shared cache, a CDN or the
      // PWA's service worker, where it would outlive the session entitled to it.
      "Cache-Control": "private, no-store",
    },
  });
}
