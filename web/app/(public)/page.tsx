import { redirect } from "next/navigation";
import { ArrowRight } from "lucide-react";

import { Hero } from "@/components/marketing/hero";
import { CapabilityStrip } from "@/components/marketing/capability-strip";
import { FeatureGrid } from "@/components/marketing/feature-grid";
import { DownloadApp } from "@/components/marketing/download-app";
import { Splash } from "@/components/marketing/splash";
import { EventCard } from "@/components/events/event-card";
import { MotionPanel } from "@/components/ui/motion-panel";
import { LinkButton } from "@/components/ui/button";
import { getFeaturedEvents } from "@/lib/api";
import { jsonLd } from "@/lib/site";

export default async function HomePage({ searchParams }: { searchParams: { login?: string } }) {
  /*
   * `requireSession()` sends expired sessions to `/?login=required#login` — an anchor to the
   * sign-in column that used to live on this page. The form is its own route now (D-290), so
   * this forwards rather than leaving them on a marketing page with no way to act on it.
   *
   * Forwarding here, rather than repointing `lib/session.ts`, keeps that file untouched: it is
   * frozen by `docs/ui-ux/do-not-change.md` §4, and its contract ("send them to `/`") is still
   * honoured. It also skips the splash for this case — nobody whose session just died wants to
   * watch a 2.6s animation first.
   */
  if (searchParams.login === "required") redirect("/login?login=required");

  let events = await getFeaturedEvents(3).catch(() => []);
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd("SoftwareApplication", { name: "Kurx", applicationCategory: "BusinessApplication" })} />
      <Splash />

      {/*
        The editorial composition (D-291). Every section below shares one container, one vertical
        rhythm (`py-16 sm:py-20`) and one header pattern — eyebrow, h2, then content — so the page
        reads as a sequence of deliberate bands rather than stacked fragments. Both themes render
        this exact tree; only the token values differ.
      */}
      <Hero />
      <CapabilityStrip />
      <FeatureGrid />

      <section className="border-b border-border bg-surface">
        <div className="container-shell py-16 sm:py-20">
          <p className="text-caption font-semibold text-accent-text">Featured</p>
          <h2 className="mt-2 max-w-2xl text-3xl font-semibold tracking-tight text-text sm:text-4xl">
            Events people can book today
          </h2>
          <div className="mt-10">
            {events.length === 0 ? (
              <p className="text-body text-muted">No featured events right now — check back soon.</p>
            ) : (
              <div className="grid gap-4 md:grid-cols-3">
                {events.map((event) => <MotionPanel key={event.slug}><EventCard event={event} /></MotionPanel>)}
              </div>
            )}
          </div>
        </div>
      </section>

      {/*
        Was a two-column section with the sign-in form in the right column. The form moved to
        `/login` (D-290), so this is the single column it always was plus the CTA that takes
        its place. `id="login"` is kept: `/#login` is linked from outside this file.
      */}
      <section id="login" className="border-b border-border bg-background">
        <div className="container-shell py-16 sm:py-20">
          <p className="text-caption font-semibold text-accent-text">Get started</p>
          <h2 className="mt-2 max-w-2xl text-3xl font-semibold tracking-tight text-text sm:text-4xl">
            Built for attendees and organizers.
          </h2>
          <p className="mt-4 max-w-2xl text-body-lg leading-8 text-muted">
            Use desktop for discovery, booking, profile, certificates, dashboards, analytics, forms,
            exports, payouts, and team operations.
          </p>
          <div className="mt-8 flex flex-wrap gap-3">
            <LinkButton href="/login">Sign in <ArrowRight size={16} aria-hidden /></LinkButton>
            <LinkButton href="/register" variant="secondary">Create an account</LinkButton>
          </div>
        </div>
      </section>

      <DownloadApp />
    </>
  );
}
