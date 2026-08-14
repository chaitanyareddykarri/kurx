import { Award, Music2, Presentation, Trophy, Wrench } from "lucide-react";

/**
 * The quiet band below the hero (D-291).
 *
 * Option 4 puts a "Trusted by event organizers worldwide" logo row here — TechFest, DevCon, EduSum,
 * DesignWeek. Those are mockup placeholders, and shipping them on a live site states that four
 * named organizations use Kurx, which is not true of any of them. Invented social proof is the one
 * thing on this page that would be a lie rather than a style choice, so the band keeps its
 * composition — a calm strip of five evenly-weighted marks — and carries something true instead:
 * the event categories the product actually models (`event_visuals.dart`, taxonomy seed).
 *
 * Swap this for real customer logos the moment there are real customers to name.
 */
const CATEGORIES = [
  ["Music", Music2],
  ["Tech", Presentation],
  ["Workshops", Wrench],
  ["Sports", Trophy],
  ["Conferences", Award]
] as const;

export function CapabilityStrip() {
  return (
    <section className="border-b border-border bg-surface">
      <div className="container-shell py-10">
        <p className="text-center text-caption text-muted">Built for every kind of event</p>
        <ul className="mt-6 flex flex-wrap items-center justify-center gap-x-10 gap-y-4">
          {CATEGORIES.map(([label, Icon]) => (
            <li key={label} className="flex items-center gap-2 text-label text-muted">
              <Icon size={16} className="text-accent-text" aria-hidden />
              {label}
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
