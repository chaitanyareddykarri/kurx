import { Award, CalendarClock, ShieldCheck } from "lucide-react";
import { Card } from "@kurx/ui";

/**
 * The public Trust panel.
 *
 * **The publication rule, applied to every signal:** a badge appears here only if it answers *"can a
 * stranger rely on this person to run or take part in an event"* AND revealing it discloses nothing
 * the person would not volunteer. Everything else is owner-only, in the Verification section.
 *
 * What that rule removed, and why — each was previously public:
 *
 * - **Email verified.** Answers "we can reach them", not "they are trustworthy". It is a
 *   contactability fact, and publishing it tells a stranger an address exists on file. Low trust
 *   value, non-zero disclosure: it fails the rule in both directions. Owner-only now.
 * - **Phone verified.** Never rendered here and never should be: *every* account exists only after
 *   an OTP login, so the badge is true for everyone. A signal that is always true carries no
 *   information and only invites the inference that a number is on file.
 * - **Verified organization / speaker / organizer.** Removed at the product owner's direction —
 *   organizations are not a concept this profile presents.
 *
 * What remains is the irreducible set: **who they are** (identity), **what they earned**
 * (certificates, each independently verifiable by code), and **how long they have been here**.
 *
 * D-221 still governs the rest: positive signals only. There is no "pending", no "rejected", and no
 * PAN / bank / penny-drop state anywhere on this surface — a component's *status* is as private as
 * its value, and "PAN rejected" on a public page is a defamatory-by-default UI.
 *
 * Absence is therefore never a negative claim. A profile with one badge is not "unverified"; it is a
 * profile that has proved one thing.
 */
type Signal = { key: string; label: string; detail: string; icon: React.ReactNode };

export function TrustPanel({
  identityVerified,
  verifiedCertificates,
  yearsOnPlatform,
}: {
  identityVerified: boolean;
  /** Null = withheld from this viewer, never zero (D-229). */
  verifiedCertificates: number | null;
  yearsOnPlatform: number;
}) {
  const signals: Signal[] = [];

  if (identityVerified)
    signals.push({
      key: "identity",
      label: "Identity verified",
      detail: "Government ID confirmed",
      icon: <ShieldCheck size={18} aria-hidden />,
    });
  // Guarded on > 0, not on non-null: "0 verified certificates" is not a trust signal, and null is
  // "hidden from you" rather than a count.
  if (verifiedCertificates !== null && verifiedCertificates > 0)
    signals.push({
      key: "certs",
      label: `${verifiedCertificates} verified ${verifiedCertificates === 1 ? "certificate" : "certificates"}`,
      detail: "Each independently verifiable by code",
      icon: <Award size={18} aria-hidden />,
    });
  if (yearsOnPlatform >= 1)
    signals.push({
      key: "tenure",
      label: `${yearsOnPlatform}+ ${yearsOnPlatform === 1 ? "year" : "years"} on Kurx`,
      detail: "Account age",
      icon: <CalendarClock size={18} aria-hidden />,
    });

  // Nothing proved yet renders nothing at all. An empty "Trust" card would say "this person has
  // verified nothing", which is a claim the platform has no business making on their page.
  if (signals.length === 0) return null;

  return (
    <Card>
      <h2 className="text-lg font-semibold text-text">Trust</h2>
      <p className="mt-1 text-sm text-muted">
        Every signal here was proved by an independent record. Nothing on this list is self-declared.
      </p>
      <ul className="mt-lg grid gap-md sm:grid-cols-2">
        {signals.map((s) => (
          <li
            key={s.key}
            className="flex items-start gap-md rounded-md border border-border bg-elevated p-md"
          >
            <span className="mt-0.5 shrink-0 text-teal">{s.icon}</span>
            <span className="min-w-0">
              <span className="block text-body font-medium text-text">{s.label}</span>
              <span className="block text-sm text-muted">{s.detail}</span>
            </span>
          </li>
        ))}
      </ul>
    </Card>
  );
}
