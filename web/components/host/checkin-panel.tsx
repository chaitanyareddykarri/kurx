"use client";

import { FormEvent, useState, useTransition } from "react";
import { CheckCircle2, XCircle } from "lucide-react";
import { Button, Field, Input, Spinner } from "@kurx/ui";
import { scanTicketAction } from "@/lib/gate-actions";

/**
 * The gate.
 *
 * The defect this carried was operational, not cosmetic: **the verdict was drawn and never
 * announced.** Someone working a door is looking at the person in front of them and at the scanner,
 * not at a paragraph that silently swaps colour — and a screen-reader user got nothing at all. The
 * result now lives in an assertive live region, because at a gate it genuinely does interrupt: the
 * next person is already stepping forward, and "already checked in" has to arrive before they do.
 *
 * `role="alert"` rather than `role="status"` for that reason, and it is the one place in this
 * redesign where assertive is the right choice — everywhere else a polite region was used precisely
 * so a page of failed sections would not fire three interruptions.
 */
export function CheckinPanel({ eventId }: { eventId: string }) {
  const [code, setCode] = useState("");
  const [count, setCount] = useState(0);
  const [result, setResult] = useState<{ ok: boolean; text: string } | null>(null);
  const [pending, start] = useTransition();

  function submit(e: FormEvent) {
    e.preventDefault();
    const c = code.trim();
    if (!c) return;
    start(async () => {
      try {
        const r = await scanTicketAction(eventId, c);
        if (r.ok && r.admitted) {
          setCount((n) => n + 1);
          // Was "Checked in ✓" — a literal check mark, which screen readers read as "check mark" or
          // drop entirely. The icon beside it is decorative and the words carry the verdict.
          setResult({ ok: true, text: "Checked in." });
        } else if (r.ok && r.isDuplicate) {
          setResult({ ok: false, text: r.message || "Already checked in." });
        } else if (r.ok) {
          setResult({ ok: false, text: "Not admitted." });
        } else {
          setResult({ ok: false, text: r.error });
        }
      } catch {
        // An unguarded scan left the operator with the previous result still on screen, which at a
        // gate reads as a fresh verdict for the person now standing there.
        setResult({ ok: false, text: "That scan didn't reach the server. Try again." });
      }
      setCode("");
    });
  }

  return (
    <div className="space-y-4">
      <form onSubmit={submit} className="flex items-end gap-2">
        <Field label="Ticket code" className="flex-1">
          <Input
            value={code}
            onChange={(e) => setCode(e.target.value)}
            placeholder="Scan or type the code"
            autoFocus
            autoComplete="off"
            autoCapitalize="off"
            spellCheck={false}
          />
        </Field>
        <Button type="submit" disabled={pending}>
          {pending ? <Spinner size={16} decorative /> : null}
          Check in
        </Button>
      </form>

      {/*
        Rendered unconditionally so the region exists before its first message — one inserted at the
        same moment as its content is not announced by most screen readers, which would have made
        this fix look done while changing nothing.
      */}
      <p role="alert" aria-live="assertive" className="min-h-6 text-sm">
        {result ? (
          <span className={`inline-flex items-center gap-1.5 ${result.ok ? "text-success" : "text-danger"}`}>
            {result.ok ? <CheckCircle2 size={16} aria-hidden /> : <XCircle size={16} aria-hidden />}
            {result.text}
          </span>
        ) : null}
      </p>

      <p role="status" className="text-xs text-muted">
        {count} checked in this session.
      </p>
    </div>
  );
}
