"use client";

import { Lock, Check, Circle } from "lucide-react";
import type { ResolvedCapability } from "@/lib/api";

/**
 * D-266 M8 — the event's modules, rendered entirely from the Capability Engine.
 *
 * **This component contains no event rules.** It does not know what a Hackathon is, that a Workshop has no
 * Leaderboard, or that a Blood Drive cannot sell tickets. It renders whatever
 * `GET .../events/{id}/capabilities` returns and shows each capability in the state the backend resolved.
 * A grep for an event-type name in `web/` is a Phase 1 failure (D-266 §9), and the reason is concrete: any
 * rule duplicated here becomes a second source of truth that drifts the moment the D12 matrix changes.
 *
 * The four states are not decoration:
 * - `required` — on, and cannot be turned off. The archetype is not itself without it.
 * - `on` / `off` — the organiser's choice.
 * - `locked` — **the archetype forbids it.** Never render a toggle; the backend would refuse. `off` means
 *   available-and-unchosen, and conflating the two is exactly what the retired Kind model could not avoid.
 */
export function ModulePicker({ capabilities }: { capabilities: ResolvedCapability[] }) {
  // Grouped by the backend's own grouping. No client-side taxonomy of modules.
  const groups = capabilities.reduce<Record<string, ResolvedCapability[]>>((acc, c) => {
    const key = c.group_slug ?? "other";
    (acc[key] ??= []).push(c);
    return acc;
  }, {});

  if (capabilities.length === 0) {
    return <p className="text-sm text-muted">No modules resolved for this event yet.</p>;
  }

  return (
    <div className="space-y-6">
      {Object.entries(groups).map(([group, caps]) => (
        <section key={group}>
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">
            {group.replace(/-/g, " ")}
          </h3>
          <ul className="grid gap-2 sm:grid-cols-2">
            {caps.map((c) => <ModuleRow key={c.slug} capability={c} />)}
          </ul>
        </section>
      ))}
    </div>
  );
}

function ModuleRow({ capability: c }: { capability: ResolvedCapability }) {
  const locked = c.state === "locked";
  const required = c.state === "required";
  const on = c.state === "on" || required;

  return (
    <li
      className={`flex items-start gap-2 rounded-lg border p-3 ${
        locked ? "border-border bg-surface opacity-60" : on ? "border-accent/50 bg-accent/5" : "border-border"
      }`}
    >
      {/*
        `aria-disabled` used to sit on this `<li>`, meaning to say that a locked module is
        unavailable rather than merely unchecked. It is not a valid attribute on the implicit
        `listitem` role, so it did nothing at all — and the row already says the same thing in words
        below ("Not available for this kind of event"), which is what actually reaches a screen
        reader. The icons are decorative beside that text.
      */}
      <span className="mt-0.5 shrink-0 text-muted">
        {locked ? (
          <Lock size={14} aria-hidden />
        ) : on ? (
          <Check size={14} aria-hidden className="text-accent-text" />
        ) : (
          <Circle size={14} aria-hidden />
        )}
      </span>
      <span className="min-w-0">
        <span className="block text-sm font-medium text-text">{c.name}</span>
        <span className="block text-xs text-muted">
          {locked
            ? "Not available for this kind of event"
            : required
              ? "Always on — this event type needs it"
              : on
                ? "On"
                : "Off"}
        </span>
      </span>
    </li>
  );
}
