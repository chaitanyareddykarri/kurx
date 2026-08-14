import { ReactNode } from "react";

/**
 * The `<p kicker/><h1/><p description/>` block every admin page repeats verbatim — one place to keep
 * that rhythm consistent, with an optional right-aligned action slot (search box, button).
 *
 * **Not promoted to `@kurx/ui`, despite the Component Migration Matrix planning it.** Web has no
 * consumer: it is `<h1>` page chrome, and the system already exports `SectionHeader` for the `<h2>`
 * in-page case. Moving a component with one caller into the shared package would be the speculative
 * abstraction `.claude/CLAUDE.md` §5 rules out. Promote it the day web needs it.
 */
export function PageHeader({
  kicker,
  title,
  description,
  actions
}: {
  kicker: string;
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div>
        {/* `accent` is the ember FILL — 2.80:1 as text on light, below AA — and this kicker sits at the
            top of all 23 admin pages. */}
        <p className="text-sm font-semibold text-accent-text">{kicker}</p>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight text-text sm:text-3xl">{title}</h1>
        {description ? <p className="mt-2 max-w-2xl text-sm text-muted">{description}</p> : null}
      </div>
      {actions ? <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div> : null}
    </header>
  );
}
