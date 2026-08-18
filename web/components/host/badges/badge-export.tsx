"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { Alert, Button, Card, Badge, Checkbox, Field, Select, Spinner } from "@kurx/ui";
import {
  downloadBadgeSheet, downloadOneBadge, generateIdCards, revokeBadge,
  type BadgeRecipient, type BadgeSize
} from "@/lib/badge-api";

type Props = {
  eventId: string;
  accessToken: string;
  sizes: BadgeSize[];
  recipients: BadgeRecipient[];
};

type Kind = "attendee" | "staff";

/**
 * Badge printing for an event (D-362).
 *
 * The whole surface is one print run: pick a size, pick who, download one PDF. There is no per-person
 * download list and no preview gallery — the output is a physical sheet, and the only thing an organiser
 * actually does here is produce it.
 */
export function BadgeExport({ eventId, accessToken, sizes, recipients }: Props) {
  const router = useRouter();
  const [sizeKey, setSizeKey] = useState(sizes[0]?.key ?? "");
  const [kinds, setKinds] = useState<Kind[]>(["attendee", "staff"]);
  const [busy, setBusy] = useState(false);
  const [issuing, setIssuing] = useState(false);
  // The user_id whose single-badge download is in flight, so only that row shows a spinner.
  const [one, setOne] = useState<string | null>(null);
  // Likewise for a revoke, which is a separate in-flight action on the same row.
  const [revoking, setRevoking] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const counts = useMemo(
    () => ({
      attendee: recipients.filter((r) => r.kind === "attendee").length,
      staff: recipients.filter((r) => r.kind === "staff").length
    }),
    [recipients]
  );

  const selectedCount = kinds.reduce((sum, k) => sum + counts[k], 0);
  const size = sizes.find((s) => s.key === sizeKey);
  const missingPhotos = recipients.filter((r) => kinds.includes(r.kind) && !r.has_photo).length;
  // An account that never filled in a display name and has no handle prints a badge with no name on it.
  // Surfaced before the run rather than discovered at the guillotine — the server refuses to print a
  // placeholder in its place (D-385), so the only visible symptom is a blank line.
  const missingNames = recipients.filter((r) => kinds.includes(r.kind) && !r.name.trim()).length;

  function toggleKind(kind: Kind, on: boolean) {
    setKinds((current) => (on ? [...new Set([...current, kind])] : current.filter((k) => k !== kind)));
  }

  async function issue() {
    setIssuing(true);
    setError(null);
    setNotice(null);
    try {
      const r = await generateIdCards(accessToken, eventId, { sizeKey, kinds });
      setNotice(
        `${r.issued} card${r.issued === 1 ? "" : "s"} issued` +
          (r.regenerated > 0 ? `, ${r.regenerated} regenerated (existing numbers kept).` : ".")
      );
      // Re-read the roster so the newly issued card numbers show without a manual refresh.
      router.refresh();
    } catch {
      setError("The cards couldn't be issued. Nothing was created — try again.");
    } finally {
      setIssuing(false);
    }
  }

  /**
   * Object URL rather than a plain href: these endpoints need a bearer token, so a navigation to one
   * would 401. Revoked immediately after the click — the blob is already in the download.
   */
  function save(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  async function print() {
    setBusy(true);
    setError(null);
    try {
      save(await downloadBadgeSheet(accessToken, eventId, { sizeKey, kinds }), `badges-${eventId}.pdf`);
    } catch {
      setError("The badge sheet couldn't be generated. Nothing was printed — try again.");
    } finally {
      setBusy(false);
    }
  }

  /** One person's badge on its own — the reprint path for a lanyard that was lost or misprinted. */
  async function printOne(r: BadgeRecipient) {
    setOne(r.user_id);
    setError(null);
    try {
      const blob = await downloadOneBadge(accessToken, eventId, r.user_id, sizeKey);
      save(blob, `badge-${(r.name || r.user_id).replace(/\W+/g, "-").toLowerCase()}.pdf`);
    } catch {
      setError(`${r.name || "That badge"} couldn't be downloaded. Nothing was printed — try again.`);
    } finally {
      setOne(null);
    }
  }

  /**
   * Revokes one issued card. Confirmed inline rather than through a dialog, and the copy states the
   * limit: this marks the card, it does not void the ticket or the assignment behind it.
   */
  async function revoke(r: BadgeRecipient) {
    if (!r.card) return;
    setRevoking(r.user_id);
    setError(null);
    setNotice(null);
    try {
      await revokeBadge(accessToken, eventId, r.user_id);
      setNotice(
        `${r.name || "That badge"} (${r.card.card_number}) is revoked. It still scans at the gate — ` +
          "revoking the card does not void their ticket or remove their assignment."
      );
      router.refresh();
    } catch {
      setError(`${r.name || "That badge"} couldn't be revoked. Nothing was changed — try again.`);
    } finally {
      setRevoking(null);
    }
  }

  if (recipients.length === 0) {
    return (
      <Card>
        <p role="status" className="text-sm text-muted">
          Nobody to badge yet. Ticket holders appear here once admissions are issued, and staff once they
          accept their assignment.
        </p>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      <Card>
        <div className="space-y-5">
          <Field label="Badge size" helper="Match this to the badge stock or card printer you're using.">
            <Select value={sizeKey} onChange={(e) => setSizeKey(e.target.value)}>
              {sizes.map((s) => (
                <option key={s.key} value={s.key}>
                  {s.label}
                </option>
              ))}
            </Select>
          </Field>

          <fieldset className="space-y-2">
            <legend className="text-sm font-medium text-text">Who to print</legend>
            <Checkbox
              checked={kinds.includes("attendee")}
              onChange={(e) => toggleKind("attendee", e.target.checked)}
              label={`Attendees (${counts.attendee})`}
            />
            <Checkbox
              checked={kinds.includes("staff")}
              onChange={(e) => toggleKind("staff", e.target.checked)}
              label={`Staff and crew (${counts.staff})`}
            />
          </fieldset>

          {missingPhotos > 0 && (
            <Alert tone="info">
              {missingPhotos} of these {selectedCount} have no profile photo. Their badges still print, with
              the photo area left blank.
            </Alert>
          )}

          {missingNames > 0 && (
            <Alert tone="warning">
              {missingNames} of these {selectedCount} have no name on their account. Their badges print with
              the name line blank — check the list below before running the job.
            </Alert>
          )}

          {notice && <Alert tone="success">{notice}</Alert>}
          {error && <Alert tone="danger">{error}</Alert>}

          <div className="flex flex-wrap items-center gap-3">
            {/* Issuing is the primary action: it creates the card records the platform can later verify
                and revoke. Downloading without issuing prints paper nothing knows about. */}
            <Button onClick={issue} disabled={issuing || busy || selectedCount === 0 || !size}>
              {issuing ? <Spinner size={16} /> : null}
              {issuing ? "Issuing…" : `Issue ${selectedCount} ID card${selectedCount === 1 ? "" : "s"}`}
            </Button>
            <Button variant="secondary" onClick={print} disabled={busy || issuing || selectedCount === 0 || !size}>
              {busy ? <Spinner size={16} /> : null}
              {busy ? "Preparing…" : "Download print sheet"}
            </Button>
            {size && (
              <p className="text-sm text-muted">
                {size.width_mm} × {size.height_mm} mm, laid out on A4 with cut guides.
              </p>
            )}
          </div>
        </div>
      </Card>

      <Card>
        <h2 className="text-sm font-semibold text-text">Who&apos;s included</h2>
        <ul className="mt-3 divide-y divide-border">
          {recipients
            .filter((r) => kinds.includes(r.kind))
            .map((r) => (
              <li key={r.user_id} className="flex items-center justify-between gap-3 py-2">
                <div className="min-w-0">
                  <p className="truncate text-sm text-text">
                    {r.name || <span className="italic text-muted">No name on this account</span>}
                  </p>
                  <p className="truncate text-xs text-muted">
                    {r.card ? (
                      <>
                        <span className="font-mono">{r.card.card_number}</span>
                        {r.subtitle ? ` · ${r.subtitle}` : ""}
                      </>
                    ) : (
                      <>Not issued{r.subtitle ? ` · ${r.subtitle}` : ""}</>
                    )}
                  </p>
                </div>
                <div className="flex shrink flex-wrap items-center justify-end gap-2 lg:shrink-0 lg:flex-nowrap">
                  {r.card?.is_revoked && <Badge tone="danger">Revoked</Badge>}
                  {r.access_level && <Badge tone="warning">{r.access_level}</Badge>}
                  <Badge tone={r.kind === "staff" ? "accent" : "muted"}>
                    {r.kind === "staff" ? "Staff" : "Attendee"}
                  </Badge>
                  {/* The reprint path: one lanyard is lost far more often than a whole run is. */}
                  <Button
                    variant="ghost"
                    onClick={() => void printOne(r)}
                    disabled={one !== null || busy || issuing || !size}
                    aria-label={`Download ${r.name || "this"} badge as PDF`}
                  >
                    {one === r.user_id ? <Spinner size={14} /> : "PDF"}
                  </Button>
                  {/* Only an issued, un-revoked card can be revoked — there is nothing to mark
                      otherwise, and the roster already shows which is which. */}
                  {r.card && !r.card.is_revoked && (
                    <Button
                      variant="ghost"
                      onClick={() => void revoke(r)}
                      disabled={revoking !== null || busy || issuing}
                      aria-label={`Revoke ${r.name || "this"} badge`}
                    >
                      {revoking === r.user_id ? <Spinner size={14} /> : "Revoke"}
                    </Button>
                  )}
                </div>
              </li>
            ))}
        </ul>
      </Card>
    </div>
  );
}
