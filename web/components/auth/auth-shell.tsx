import { KeyRound, ShieldCheck, SmartphoneNfc } from "lucide-react";

import { Atmosphere } from "@/components/marketing/atmosphere";

/**
 * The frame both signed-out auth screens sit in (D-384).
 *
 * `/login` and `/register` are the same decision seen from two sides, and they used to look like two
 * different products: one centred a card under a heading inside the public shell, the other rendered
 * a bare `<main>` with no navigation at all. One component now owns that layout, so the two cannot
 * drift again — which is the reason it exists, not a guess at future reuse.
 *
 * `Atmosphere` is imported from `components/marketing/` because that is where the ambient primitive
 * already lives; D-384 §1 is what makes using it here legitimate, by moving the boundary from
 * "marketing" to "signed out". The directory name is where the code sits, not the rule.
 *
 * **No `order` juggling.** DOM order is heading → supporting copy → form, which is the correct
 * reading order on a phone (the heading has to precede the form) and already the correct visual
 * order on a wide screen, where the grid puts the first column on the left. Nothing in the left
 * column is focusable — a heading, a paragraph and a static list — so putting it first costs a
 * keyboard user no tab stops, and reordering to "fix" that would only break the mobile reading order.
 */
const ASSURANCES = [
  [ShieldCheck, "A password, then a second factor", "Never a code on its own."],
  [KeyRound, "Passkeys supported", "Sign in with the device you already unlock."],
  [SmartphoneNfc, "You choose what is remembered", "Trust a browser only when you say so."]
] as const;

export function AuthShell({
  title,
  subtitle,
  children
}: {
  title: string;
  subtitle: string;
  children: React.ReactNode;
}) {
  return (
    // `border-b`, like every band on the home page. Without it the ambient wash simply stopped at
    // the section's box and the footer began, which reads as a rendering seam rather than a boundary.
    <section className="relative border-b border-border">
      <Atmosphere grid />

      <div className="container-shell relative grid gap-8 py-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,440px)] lg:items-center lg:gap-16 lg:py-20">
        <div>
          <h1 className="text-3xl font-semibold tracking-tight text-text sm:text-4xl">{title}</h1>
          <p className="mt-3 max-w-md text-body-lg leading-8 text-muted">{subtitle}</p>

          {/*
            Statements about how this screen actually works, not marketing. Each one names something
            visible in the panel beside it — the password field and its second-factor step (D-182),
            the passkey option, and the "Remember this browser" checkbox — so a visitor deciding
            whether to trust the form can check every claim against it.

            Desktop only. On a phone this sits between the heading and the form, which is prose in
            front of the task; the form is what a small screen should show.
          */}
          <ul className="mt-10 hidden max-w-md space-y-5 lg:block">
            {ASSURANCES.map(([Icon, label, detail]) => (
              <li key={label} className="flex gap-3.5">
                <span className="mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-md border border-border bg-elevated text-accent-text shadow-sm">
                  <Icon size={16} aria-hidden />
                </span>
                <span>
                  <span className="block text-label text-text">{label}</span>
                  <span className="mt-0.5 block text-caption text-muted">{detail}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>

        <div className="w-full">{children}</div>
      </div>
    </section>
  );
}
