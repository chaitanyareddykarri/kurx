import { NextResponse } from "next/server";
import { currentSession } from "@/lib/session";
import { siteConfig } from "@/lib/site";

/// Forwards a run's creation to `POST /v1/events/{id}/certificate-batches` (D-355, Phase 7).
///
/// Same reasoning as the participant preview alongside it: this carries a file, and a file goes through a
/// route handler rather than a Server Action. The run's name, design and column mapping ride as query
/// parameters because the body is already spoken for by multipart.
export async function POST(request: Request, { params }: { params: { id: string } }) {
  const session = await currentSession();
  if (!session) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  const incoming = new URL(request.url).searchParams;
  const query = new URLSearchParams({
    name: incoming.get("name") ?? "",
    templateId: incoming.get("templateId") ?? "",
    mapping: incoming.get("mapping") ?? "{}",
  });

  const body = await request.arrayBuffer();

  const upstream = await fetch(
    `${siteConfig.apiBaseUrl}/v1/events/${params.id}/certificate-batches?${query}`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        "Content-Type": request.headers.get("content-type") ?? "application/octet-stream",
      },
      body,
      cache: "no-store",
    });

  const text = await upstream.text();
  try {
    return NextResponse.json(JSON.parse(text), { status: upstream.status });
  } catch {
    return NextResponse.json(
      { error: "upstream_unreadable", detail: text.slice(0, 500) }, { status: upstream.status });
  }
}
