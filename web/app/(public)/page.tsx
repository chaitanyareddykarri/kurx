import { redirect } from "next/navigation";
import { ArrowRight } from "lucide-react";

import { Hero } from "@/components/marketing/hero";
import { CapabilityStrip } from "@/components/marketing/capability-strip";
import { FeatureGrid } from "@/components/marketing/feature-grid";
import { Represent } from "@/components/marketing/represent";
import { Journey } from "@/components/marketing/journey";
import { DownloadApp } from "@/components/marketing/download-app";
import { Splash } from "@/components/marketing/splash";
import { Atmosphere } from "@/components/marketing/atmosphere";
import { SpatialCard, SpatialRoot } from "@/components/marketing/spatial";
import { EventCard } from "@/components/events/event-card";
import { MotionPanel } from "@/components/ui/motion-panel";
import { LinkButton } from "@/components/ui/button";
import { getFeaturedEvents, getUpcomingEvents } from "@/lib/api";
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

  /*
   * Curated first, upcoming as the fallback (D-383).
   *
   * `IsFeatured` is an admin curation flag, so `/v1/events/featured` legitimately answers `[]` until
   * somebody curates — which is the state EVERY new deployment starts in. Asking only that question
   * put "No featured events right now" on the one section of this page that shows real data, on a
   * platform that had published, bookable events. The endpoint was never the problem; the question
   * was.
   *
   * Sequential rather than parallel on purpose: the second call is only made when the first comes
   * back empty, so a curated homepage pays for exactly one request.
   */
  const featured = await getFeaturedEvents(3).catch(() => []);
  const events = featured.length > 0 ? featured : await getUpcomingEvents(3).catch(() => []);
  const curated = featured.length > 0;
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd("SoftwareApplication", { name: "Kurx", applicationCategory: "BusinessApplication" })} />
      <Splash />

      {/*
        The scroll reveals must not be able to hide the page (D-380 §2).

        `MotionPanel` is a framer component with `initial={{ opacity: 0, y: 12 }}`, and framer writes
        that initial state into the server-rendered markup as an inline style — this page ships 11
        elements at `opacity:0` before hydration, covering the representation, platform, journey and
        featured-events blocks. That is correct while JavaScript is on its way and wrong the moment it
        never arrives: with scripting off or broken, a third of the page is present in the HTML and
        invisible on screen. The promise this exception was granted under is that the page still says
        what Kurx is when every effect fails, so the failure case gets a real branch.

        `<noscript>` is the whole fix: the browser applies the rule only when scripting is off, so the
        hydrated path is untouched and there is no flag, no state and nothing to keep in sync. It is
        scoped by `[style*="opacity:0"]` because that inline style is precisely what framer wrote and
        precisely what needs undoing.
      */}
      <noscript>
        <style>{`[style*="opacity:0"]{opacity:1!important;transform:none!important}`}</style>
      </noscript>

      {/*
        The spatial composition (D-380), superseding the flat editorial one.
        
        Seven scenes that read as one progression — what Kurx is, what kind of events it holds, real
        events to look at, how organizing actually works here, what the platform does, where
        participation ends up, and the way in. The shared rhythm survives the change: one container,
        one `py-16 sm:py-20`, one eyebrow → h2 → content header pattern, and alternating
        background/surface bands so no two neighbours share a ground.

        `SpatialRoot` is the only thing wrapping them, and it is a framer `MotionConfig` in
        `reducedMotion="user"` — one provider that switches every animated descendant on this page,
        including the shared `MotionPanel`, to opacity-only. Its children are still server components.
      */}
      <SpatialRoot>
        <Hero />
        <CapabilityStrip />

        <section className="relative border-b border-border bg-background">
          <Atmosphere />
          <div className="container-shell relative py-16 sm:py-20">
            <p className="text-caption font-semibold text-accent-text">{curated ? "Featured" : "Upcoming"}</p>
            <h2 className="mt-2 max-w-2xl text-3xl font-semibold tracking-tight text-text sm:text-4xl">
              Events people can book today
            </h2>
            <div className="mt-10">
              {events.length === 0 ? (
                /*
                  Not "no featured events": by the time this renders, curation has already been asked
                  and answered, and so has the upcoming fallback (D-383). The line has to be true when
                  nothing is curated, when nothing is published, AND when the API is unreachable —
                  `getUpcomingEvents` is caught to `[]` too, so a backend outage lands here. Naming
                  curation in that state explains the one thing already ruled out.
                */
                <p className="text-body text-muted">No events to show right now — check back soon.</p>
              ) : (
                /*
                  The one section on this page showing real data. `getFeaturedEvents` and `EventCard`
                  are untouched — `SpatialCard` wraps the shared card rather than forking it, so the
                  same component still serves discovery and the host workspace, and every `/e/{slug}`
                  link behaves exactly as it did.
                */
                <div className="grid gap-4 md:grid-cols-3">
                  {events.map((event) => (
                    <MotionPanel key={event.slug}>
                      <SpatialCard>
                        <EventCard event={event} />
                      </SpatialCard>
                    </MotionPanel>
                  ))}
                </div>
              )}
            </div>
          </div>
        </section>

        <Represent />
        <FeatureGrid />
        <Journey />

        {/*
          Was a two-column section with the sign-in form in the right column. The form moved to
          `/login` (D-290), so this is the single column it always was plus the CTA that takes
          its place. `id="login"` is kept: `/#login` is linked from outside this file.
        */}
        <section id="login" className="relative border-b border-border bg-background">
          <Atmosphere />
          <div className="container-shell relative py-20 sm:py-24">
            <p className="text-caption font-semibold text-accent-text">Get started</p>
            <h2 className="mt-2 max-w-3xl text-3xl font-semibold tracking-tight text-text sm:text-4xl lg:text-5xl">
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
      </SpatialRoot>
    </>
  );
}
