"use client";

/**
 * Where you are, what you have done, and what comes next (D-359).
 *
 * A certificate goes through five stages and a first-time user has no way to know that. The rail says it
 * outright, in order, in plain words — so "what do I do now" is answered by looking rather than by asking.
 *
 * **Status is never carried by colour alone.** A finished step is marked with a tick *and* the word Done;
 * the current one is marked with a ring, a bold label *and* the word Now, and carries `aria-current`. Read
 * with no colour at all — greyscale, a cheap projector, most kinds of colour blindness — every state is
 * still legible.
 *
 * A step you can reach is a button; one you cannot is plain text with a reason. Nothing here silently does
 * nothing when clicked.
 */

export type StepId = "upload" | "detect" | "edit" | "preview" | "download";

export interface Step {
  id: StepId;
  /** Short enough to read at a glance, and a verb, because each one is something you do. */
  label: string;
  /** One sentence. What happens at this step. */
  hint: string;
  done: boolean;
  /** Whether it can be jumped to. A step nobody can act on yet is shown but not offered. */
  enabled: boolean;
}

export function CertificateSteps({ steps, current, onGo }: {
  steps: Step[];
  current: StepId;
  onGo: (id: StepId) => void;
}) {
  return (
    <nav aria-label="Steps to make your certificate">
      <ol className="flex flex-wrap items-stretch gap-2">
        {steps.map((step, index) => {
          const isCurrent = step.id === current;
          const state = isCurrent ? "Now" : step.done ? "Done" : "To do";

          const body = (
            <>
              <span
                aria-hidden
                className={`grid h-11 w-11 shrink-0 place-items-center rounded-full text-base font-bold ${
                  isCurrent
                    ? "bg-accent text-on-accent ring-4 ring-accent/25"
                    : step.done
                      ? "bg-success/15 text-success ring-1 ring-success/40"
                      : "bg-elevated text-muted ring-1 ring-border"
                }`}
              >
                {step.done && !isCurrent ? "✓" : index + 1}
              </span>

              <span className="min-w-0 text-left">
                <span className={`block text-sm ${isCurrent ? "font-bold text-text" : "font-semibold text-text"}`}>
                  {step.label}
                </span>
                {/* The word, not just the colour. */}
                <span className="block text-[11px] text-muted">{state}</span>
              </span>
            </>
          );

          const shared = "flex flex-1 basis-44 items-center gap-3 rounded-xl border px-3 py-3 min-h-[68px]";

          return (
            <li key={step.id} className="flex flex-1 basis-44">
              {step.enabled ? (
                <button
                  type="button"
                  onClick={() => onGo(step.id)}
                  aria-current={isCurrent ? "step" : undefined}
                  // Says what it does — go there — rather than repeating the step's name. A rail item
                  // reading just "Find text" is indistinguishable from the button that actually finds it,
                  // and clicking the wrong one appears to do nothing at all. The visible words are kept
                  // inside the label, so what is read still matches what is seen (WCAG 2.5.3).
                  aria-label={`Go to step ${index + 1}: ${step.label} (${state})`}
                  title={step.hint}
                  className={`${shared} text-left transition ${
                    isCurrent
                      ? "border-accent bg-accent/5"
                      : "border-border bg-surface hover:border-accent/50 hover:bg-elevated"
                  } focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent`}
                >
                  {body}
                </button>
              ) : (
                <div
                  className={`${shared} border-dashed border-border bg-surface/60 opacity-70`}
                  aria-current={isCurrent ? "step" : undefined}
                >
                  {body}
                </div>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
