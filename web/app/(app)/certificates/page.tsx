import { Award, ShieldCheck } from "lucide-react";
import { Card, EmptyState, LinkButton } from "@kurx/ui";
import { getPublicProfileCertificates, section } from "@/lib/api";
import { requireSession } from "@/lib/session";

export const metadata = { title: "Certificates" };

export default async function CertificatesPage() {
  const session = await requireSession();
  const username = session.me.username;

  /*
   * These are the caller's OWN certificates, read through the *public profile* endpoint — so they
   * arrive filtered by whatever the caller has chosen to publish. Someone who keeps their
   * certificates private saw "No certificates yet" on their own page.
   *
   * The viewer token is passed for that reason: the same read is viewer-aware (D-229/C1), and
   * sending it is what makes the owner entitled to their own rows. Without a username there is no
   * public profile to read at all, which is a different situation from having none and now says so.
   */
  const result = username
    ? await section(getPublicProfileCertificates(username, session.accessToken))
    : null;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-semibold text-text">Certificates</h1>
        <p className="mt-2 text-sm text-muted">
          Your verified credentials. Open one to download the PDF, share its verification link, or view it in the app.
        </p>
      </div>

      {result === null ? (
        <EmptyState
          icon={<Award size={32} />}
          title="Your profile needs a username"
          message="Certificates are listed against your public profile, so this page needs one before it can show them."
          action={<LinkButton href="/settings/identity" variant="secondary">Choose a username</LinkButton>}
        />
      ) : result.state !== "ok" ? (
        <div role="status" className="rounded-lg border border-dashed border-border bg-surface p-6 text-center">
          <p className="text-body text-text">Your certificates couldn&apos;t be loaded.</p>
          <p className="mt-1 text-sm text-muted">
            This is a temporary problem on our side, not a change to your credentials. Try refreshing.
          </p>
        </div>
      ) : result.data.length === 0 ? (
        <EmptyState
          icon={<Award size={32} />}
          title="No certificates yet"
          message="They appear here once an organizer issues one to you."
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {result.data.map((c) => (
            <Card key={c.id}>
              <div className="flex items-center gap-2">
                <Award size={18} aria-hidden className="text-accent-text" />
                <h2 className="font-semibold text-text">{c.event_title}</h2>
              </div>
              <p className="mt-1 text-sm text-muted">
                Issued {new Date(c.issued_at).toLocaleDateString("en-IN", { dateStyle: "medium" })}
              </p>
              <p className="mt-1 text-sm text-muted">
                Code: <span className="font-mono text-text">{c.verify_code}</span>
              </p>
              <div className="mt-3">
                <LinkButton href={`/verify/${c.verify_code}`} variant="secondary">
                  <ShieldCheck size={16} aria-hidden /> Verify &amp; download
                </LinkButton>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
