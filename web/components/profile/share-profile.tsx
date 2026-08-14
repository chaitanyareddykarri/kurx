"use client";

import { useState } from "react";
import { Check, Copy, Link2, Share2 } from "lucide-react";
import { QRCodeSVG } from "qrcode.react";
import { Button, Dialog, useToast } from "@kurx/ui";

import { siteConfig } from "@/lib/site";

/**
 * Share + QR for a public profile.
 *
 * **The QR is encoded client-side, from `siteConfig.url`.** A server-rendered PNG was the obvious
 * way to keep web and Flutter byte-identical, but the backend has no configured public web origin —
 * it would have had to guess one from the request host, which for an API call is the *API* host, so
 * every scanned code would open a URL that does not serve profiles. The client is the only party
 * that reliably knows its own origin. `qrcode.react` is already a dependency here; Flutter uses
 * `qr_flutter` for the same three lines. Two small call sites, each correct, beat one shared one
 * that encodes the wrong host.
 *
 * `navigator.share` is used where the platform provides it (every mobile browser, and it is the
 * gesture a phone user expects); the copy button is the always-present fallback rather than a
 * second-class path — `share` is absent on most desktop browsers and rejects on user cancel.
 */
export function ShareProfile({ username, name }: { username: string; name: string }) {
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const toast = useToast();

  const url = `${siteConfig.url}/u/${username}`;

  async function copy() {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      // Reverts on its own so the button never stays stuck in the confirmed state after the
      // clipboard has moved on to something else.
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast("Could not copy the link.", "error");
    }
  }

  async function share() {
    // Feature-detected rather than UA-sniffed. An AbortError is the user dismissing the sheet —
    // not a failure, and surfacing it as one would be a lie.
    if (typeof navigator !== "undefined" && "share" in navigator) {
      try {
        await navigator.share({ title: name, text: `${name} on Kurx`, url });
        return;
      } catch (err) {
        if ((err as Error)?.name === "AbortError") return;
      }
    }
    void copy();
  }

  return (
    <>
      <Button variant="secondary" onClick={() => setOpen(true)}>
        <Share2 size={15} aria-hidden /> Share
      </Button>

      <Dialog open={open} onClose={() => setOpen(false)} title={`Share @${username}`}>
        <div className="flex flex-col items-center gap-lg">
          {/* White plate and explicit black modules regardless of theme: a QR is read by a camera,
              and inheriting dark-mode colours inverts it, which many readers refuse to decode. */}
          {/* The accessible name sits on the wrapper: the SVG itself takes no `title` prop, and an
              unlabelled graphic here would announce as nothing at all. */}
          <div
            role="img"
            aria-label={`QR code linking to the profile of ${name}`}
            className="rounded-lg border border-border bg-white p-3"
          >
            <QRCodeSVG
              value={url}
              size={200}
              bgColor="#ffffff"
              fgColor="#000000"
              // Medium recovery — the code stays scannable when partly obscured on a phone screen
              // without inflating module count the way High would at this size.
              level="M"
            />
          </div>

          <p className="text-center text-sm text-muted">
            Point a camera at this code to open {name}&apos;s profile.
          </p>

          <div className="flex w-full items-center gap-sm rounded-md border border-border bg-elevated px-3 py-2">
            <Link2 size={15} aria-hidden className="shrink-0 text-muted" />
            <span className="truncate text-sm text-text">{url}</span>
          </div>

          <div className="flex w-full flex-wrap gap-sm">
            <Button onClick={share} className="flex-1">
              <Share2 size={15} aria-hidden /> Share
            </Button>
            <Button variant="secondary" onClick={copy} className="flex-1">
              {copied ? <Check size={15} aria-hidden /> : <Copy size={15} aria-hidden />}
              {copied ? "Copied" : "Copy link"}
            </Button>
          </div>
        </div>
      </Dialog>
    </>
  );
}
