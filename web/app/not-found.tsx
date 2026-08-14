import Link from "next/link";
import { Compass } from "lucide-react";

/**
 * 404 for the whole site.
 *
 * Web had none, so every `notFound()` — and D-018 routes a hidden resource here deliberately, so
 * this is not a rare page — rendered Next's default: the word "404" on a white background, with no
 * navigation out. That is a dead end in a product whose whole job is to move someone toward an event.
 *
 * The copy has to work for both things that land here: something genuinely missing, and something
 * the viewer is not allowed to see. It cannot say "this doesn't exist", because for the second case
 * that is a lie the 404 exists precisely to avoid telling apart from a 403.
 */
export default function NotFound() {
  return (
    <main className="container-shell py-20">
      <div className="mx-auto flex max-w-md flex-col items-center gap-4 text-center">
        <Compass size={32} className="text-muted" aria-hidden />
        <h1 className="text-xl font-semibold text-text">We couldn&apos;t find that page</h1>
        <p className="text-sm text-muted">
          The link may be out of date, or the page may not be available to you.
        </p>
        {/* Was a "Browse events" CTA pointing at `/events`, which does not exist — public discovery
            lives on the homepage, and `/discover` is behind auth. A 404 whose primary button leads
            to another 404 is worse than one with a single honest way out. Found in Phase 47 by
            loading the page; the Phase 44 test had asserted the broken href, so the test agreed
            with the bug. */}
        <Link
          href="/"
          className="inline-flex min-h-11 items-center rounded-md bg-accent px-5 text-sm font-medium text-on-accent"
        >
          Go to the homepage
        </Link>
      </div>
    </main>
  );
}
