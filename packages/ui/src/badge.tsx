import { ReactNode } from "react";

/**
 * A small status pill (event state, role, count) — the web twin of mobile's KurxBadge.
 *
 * **The tinted background is gone, and that was a measurement, not a taste call.**
 * Every tone previously rendered its own token on a 15% tint of itself. The tint
 * eats exactly the contrast headroom the token was solved for, and all five tones
 * measured 3.61-4.45:1 in light and 3.70-4.62:1 in dark — every one below AA, in
 * both themes, and still below it at 10%.
 *
 * Tones now sit on `elevated` with a tone-coloured border, so the label's contrast
 * is precisely what Phase 4 guaranteed (`elevated` is the worst of the three
 * surface steps, and every text token was solved against it). Tone is carried by
 * the border *and* the text colour, never by colour alone — badges also always
 * contain words.
 *
 * `accent` uses `accent-text`, not `accent`: the ember fill is 2.80:1 on light and
 * is not a text colour.
 */
const tones = {
  neutral: "border-border-strong text-text",
  accent: "border-accent/50 text-accent-text",
  /** Attests — provenance, verification, certificates. Never generic success (D-286). */
  teal: "border-teal/50 text-teal",
  success: "border-success/50 text-success",
  warning: "border-warning/50 text-warning",
  danger: "border-danger/50 text-danger",
  muted: "border-border text-muted"
} as const;

export function Badge({
  children,
  tone = "neutral",
  icon
}: {
  children: ReactNode;
  tone?: keyof typeof tones;
  icon?: ReactNode;
}) {
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-pill border bg-elevated px-2.5 py-0.5 text-micro ${tones[tone]}`}
    >
      {icon}
      {children}
    </span>
  );
}

/** Host trust tiers (Tier 1 new / Tier 2 verified / Tier 3 power) — visual only. */
export function HostTierBadge({ tier }: { tier: 1 | 2 | 3 }) {
  const map = {
    1: { label: "New host", tone: "muted" as const },
    // Verified is an attestation, so it takes teal rather than ember (D-286).
    2: { label: "Verified", tone: "teal" as const },
    3: { label: "Power host", tone: "success" as const }
  };
  const { label, tone } = map[tier];
  return <Badge tone={tone}>{label}</Badge>;
}
