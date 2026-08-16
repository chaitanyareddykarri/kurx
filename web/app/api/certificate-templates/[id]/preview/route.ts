import { NextResponse } from "next/server";
import { currentSession } from "@/lib/session";
import { siteConfig } from "@/lib/site";

/// Fetches a rendered sample of a design from `GET /v1/certificate-templates/{id}/preview` (D-359).
///
/// A route handler rather than a Server Action because what comes back is a **file** — a PNG or a PDF —
/// and an action would have to base64 it through its own serialisation to hand it to the browser. Here the
/// bytes pass straight through, which is also what lets the same URL be given to an `<img>` and to a
/// download.
///
/// The point of using it at all: this is the SAME renderer that issues certificates, so Preview shows what
/// will actually print rather than the browser's approximation of it. Fonts, line breaking and spacing on
/// the editing canvas are the browser's; here they are the real thing.
///
/// Authorization is not re-implemented — the API owns it, and its answer including the status is passed
/// through verbatim.
export async function GET(request: Request, { params }: { params: { id: string } }) {
  const session = await currentSession();
  if (!session) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  const format = new URL(request.url).searchParams.get("format") === "pdf" ? "pdf" : "png";

  const upstream = await fetch(
    `${siteConfig.apiBaseUrl}/v1/certificate-templates/${params.id}/preview?format=${format}`,
    {
      headers: { Authorization: `Bearer ${session.accessToken}` },
      cache: "no-store",
    });

  if (!upstream.ok) {
    // The API's problem details are JSON; the caller only ever needs to know that it did not work, and
    // says so in its own words. Passing the status through keeps 403 and 404 distinguishable.
    return NextResponse.json({ error: "preview_failed" }, { status: upstream.status });
  }

  return new NextResponse(await upstream.arrayBuffer(), {
    status: 200,
    headers: {
      "Content-Type": upstream.headers.get("content-type") ?? "application/octet-stream",
      "Content-Disposition": upstream.headers.get("content-disposition") ?? "inline",
      // A sample is regenerated from the current design every time; a cached one would show yesterday's
      // layout after an edit, which is the one thing a preview must never do.
      "Cache-Control": "no-store",
    },
  });
}
