import { ShieldCheck, Sparkles, PenLine } from "lucide-react";

/**
 * The provenance marker (D-221). Every profile value belongs to exactly one of three categories, and
 * the badge is driven by the server's `_meta` map rather than by the field's name — so a client can
 * never accidentally present a self-declared claim as proof.
 *
 * `verified`      — a system row proves it. Never editable.
 * `derived`       — computed from verified rows. Never editable, always evidence-backed.
 * `self_declared` — the user typed it. Shown, but never counted as evidence.
 */
const VARIANTS = {
  verified: { icon: ShieldCheck, label: "Verified", hint: "From records Kurx can prove", tone: "text-accent-text" },
  derived: { icon: Sparkles, label: "Derived", hint: "Computed from verified activity", tone: "text-accent-text" },
  self_declared: { icon: PenLine, label: "Self-declared", hint: "Written by this person, not verified", tone: "text-muted" }
} as const;

export type Provenance = keyof typeof VARIANTS;

export function ProvenanceBadge({ source, className = "" }: { source: string; className?: string }) {
  const variant = VARIANTS[source as Provenance];
  // An unknown category renders nothing rather than guessing — a wrong badge is worse than none.
  if (!variant) return null;

  const { icon: Icon, label, hint, tone } = variant;
  return (
    // The hint used to live in `title` alone. A `title` on a non-focusable span reaches neither the
    // keyboard nor touch, and this badge is the entire mechanism by which a reader tells proof from a
    // claim — the one thing on a profile that must not be pointer-only. It is now real text: visible
    // on hover for pointers, and always present for assistive tech.
    <span
      className={`group relative inline-flex items-center gap-1 text-[11px] font-medium ${tone} ${className}`}
    >
      <Icon size={11} aria-hidden />
      {label}
      <span className="sr-only"> — {hint}</span>
      <span
        aria-hidden
        className="pointer-events-none absolute bottom-full left-1/2 z-overlay mb-1.5 hidden w-max max-w-[16rem] -translate-x-1/2 rounded-md border border-border-strong bg-elevated px-2.5 py-1.5 text-caption text-text shadow-md group-hover:block"
      >
        {hint}
      </span>
    </span>
  );
}
