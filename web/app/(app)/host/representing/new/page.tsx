import { Card, LinkButton } from "@kurx/ui";
import { CreateOrgForm } from "@/components/host/create-org-form";

// Request to represent a not-yet-registered organization (event-first, D-074/D-075). Submitting stages a
// hidden org an admin verifies; the caller becomes a Verified Representative on approval — never an owner,
// because organizations have no account.
export default function RequestRepresentationPage({
  searchParams
}: {
  /// The shape Next actually passes. Typing it `{ returnTo?: string }` would be a lie the compiler
  /// believes: a repeated `?returnTo=a&returnTo=b` arrives as an ARRAY, and `.startsWith` on it throws
  /// at request time — a 500 on a URL anyone can type.
  searchParams?: { [key: string]: string | string[] | undefined };
}) {
  // D-382 — the event context, when the caller arrived from one. Only a `/host/` path is carried, and
  // the redirect target is re-validated in the action; this is presentation, not the guard.
  const raw = searchParams?.returnTo;
  const candidate = Array.isArray(raw) ? raw[0] : raw;
  const returnTo = candidate?.startsWith("/host/") ? candidate : undefined;

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-3xl font-semibold">Request to represent an organization</h1>
        <p className="mt-1 text-sm text-muted">
          A college, company, club, or community not yet on Kurx. This submits a representation request an admin
          verifies before it joins the registry — you become a Verified Representative on approval.
        </p>
      </div>
      <Card>
        <CreateOrgForm returnTo={returnTo} />
      </Card>
      {/* Leaving mid-request must not mean losing the event: the way back is on screen the whole time,
          not only after a successful submit. */}
      {returnTo ? (
        <LinkButton href={returnTo} variant="secondary">Back to your event</LinkButton>
      ) : null}
    </div>
  );
}
