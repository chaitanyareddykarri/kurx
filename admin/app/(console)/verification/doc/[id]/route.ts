import { NextRequest, NextResponse } from "next/server";
import { currentSession } from "@/lib/session";
import { getVerificationDocViewUrl } from "@/lib/api";

// Reviewer-only redirect to a short-lived presigned URL for a verification evidence document (D-055 G2).
// The httpOnly session token can't ride a plain <a> to the API, so this same-origin handler makes the
// authed call server-side and redirects to storage. The backend still enforces the reviewer role.
export async function GET(_req: NextRequest, { params }: { params: { id: string } }) {
  const session = await currentSession();
  if (!session) return new NextResponse("Unauthorized", { status: 401 });
  try {
    const url = await getVerificationDocViewUrl(session.accessToken, params.id);
    return NextResponse.redirect(url);
  } catch {
    return new NextResponse("Document not available.", { status: 404 });
  }
}
