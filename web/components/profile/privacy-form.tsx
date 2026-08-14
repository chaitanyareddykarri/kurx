"use client";

import { useFormState, useFormStatus } from "react-dom";
import { updatePrivacyAction } from "@/lib/profile-actions";
import { Button } from "@/components/ui/button";
import { PROFILE_SECTIONS, SECTION_TIERS, type PrivacyFlags, type SectionTier } from "@/lib/api";

const TIER_LABELS: Record<SectionTier, string> = {
  public: "Anyone",
  connections: "My connections",
  event_participants: "People I've shared an event with",
  only_me: "Only me"
};

// Copy per section. `profile` is the whole-profile gate — restricting it makes the page read as
// not-found to anyone below the tier, so it is described differently from a section toggle.
const SECTION_COPY: Record<(typeof PROFILE_SECTIONS)[number], { label: string; hint: string }> = {
  profile: {
    label: "My profile page",
    hint: "Below this level your profile is not found at all — it can't be told apart from one that doesn't exist."
  },
  events: { label: "Events I organized", hint: "Public events you ran or helped run." },
  attended: { label: "Events I attended", hint: "Events you checked in to. Hidden by default." },
  certificates: { label: "Certificates", hint: "Each certificate stays verifiable by its code even when hidden here." },
  achievements: { label: "Achievements", hint: "Competition wins and recognitions from verified results." },
  organizations: { label: "Organizations", hint: "Verified memberships and the roles you hold." },
  timeline: { label: "Professional timeline", hint: "Your chronological milestones." },
  network: { label: "Connections", hint: "You can also hide a single connection from the list." },
  metrics: { label: "Activity metrics", hint: "Counts, rates and your Event DNA breakdown." },
  contributions: { label: "Contribution heatmap", hint: "Your day-by-day activity over the last year." }
};

// The four booleans map onto sections; a tier the boolean can't express reads as its nearest
// restriction. Falls back to today's defaults when the server omits `sections` entirely.
function currentTier(privacy: PrivacyFlags, section: string): SectionTier {
  const stored = privacy.sections?.[section];
  if (stored && (SECTION_TIERS as readonly string[]).includes(stored)) return stored as SectionTier;
  switch (section) {
    case "profile": return privacy.profile_public ? "public" : "only_me";
    case "attended": return privacy.show_attended ? "public" : "only_me";
    case "certificates": return privacy.show_certificates ? "public" : "only_me";
    case "network": return privacy.show_allies ? "public" : "only_me";
    default: return "public";
  }
}

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Saving…" : "Save privacy"}</Button>;
}

export function PrivacyForm({ initial }: { initial: PrivacyFlags }) {
  const [state, formAction] = useFormState(updatePrivacyAction, null);

  return (
    <form action={formAction}>
      <div className="divide-y divide-border">
        {PROFILE_SECTIONS.map((section) => {
          const { label, hint } = SECTION_COPY[section];
          return (
            <div key={section} className="flex flex-wrap items-start justify-between gap-3 py-3">
              <div className="min-w-0 flex-1">
                <label htmlFor={`sec-${section}`} className="block text-sm font-medium text-text">{label}</label>
                <p className="text-xs text-muted">{hint}</p>
              </div>
              <select
                id={`sec-${section}`}
                name={`section_${section}`}
                defaultValue={currentTier(initial, section)}
                className="h-9 shrink-0 rounded-md border border-border-strong bg-background px-2 text-sm text-text"
              >
                {SECTION_TIERS.map((tier) => (
                  <option key={tier} value={tier}>{TIER_LABELS[tier]}</option>
                ))}
              </select>
            </div>
          );
        })}
      </div>
      <div className="mt-4 flex items-center gap-3">
        <SubmitButton />
        {state && "error" in state ? <p className="text-sm text-danger">{String(state.error)}</p> : null}
        {state && "ok" in state ? <p className="text-sm text-accent">Privacy saved.</p> : null}
      </div>
    </form>
  );
}
