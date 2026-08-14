"use client";

import { useState } from "react";

/**
 * A ticket's real, scannable QR — the one the gate reads
 * (`POST /v1/gate/{eventId}/scan` takes the ticket code this encodes).
 *
 * The image is fetched through `/api/ticket-qr/{code}`, a server route, because the upstream PNG
 * endpoint requires a bearer token an `<img>` cannot send. Deliberately a plain `<img>` and not
 * `next/image`: the optimiser re-encodes and rescales, and a QR that has been resampled is a QR that
 * may not scan.
 *
 * When the image cannot be produced the code itself is shown instead. That is not a decorative
 * fallback — a gate can be opened by typing the code, so the failure mode still admits the holder.
 */
export function TicketQr({ code, eventTitle }: { code: string; eventTitle: string }) {
  const [failed, setFailed] = useState(false);

  if (failed) {
    return (
      <div className="flex aspect-square w-full items-center justify-center rounded-md border border-dashed border-border-strong bg-surface p-2 text-center">
        <p className="text-caption text-muted">
          The QR image couldn&apos;t be loaded. Show the code below at the gate.
        </p>
      </div>
    );
  }

  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`/api/ticket-qr/${code}`}
      alt={`Entry QR code for ${eventTitle}`}
      width={160}
      height={160}
      onError={() => setFailed(true)}
      // A QR must stay square and crisp; `bg-white` because scanners need the light quiet zone
      // regardless of which theme the holder is using.
      className="aspect-square w-full rounded-md bg-white p-1.5"
    />
  );
}
