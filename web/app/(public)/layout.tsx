import { Footer } from "@/components/layout/footer";
import { PublicNav } from "@/components/layout/public-nav";

/**
 * The public marketing shell.
 *
 * Gains the same skip link as the signed-in shell (no page in web or admin had
 * one) and a `<main>` landmark, plus the footer — without which `/privacy` and
 * `/terms` were reachable only by typing their URLs.
 */
export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-toast focus:rounded-md focus:border focus:border-border-strong focus:bg-surface focus:px-4 focus:py-2 focus:text-label focus:text-text"
      >
        Skip to content
      </a>
      <PublicNav />
      <main id="main-content" tabIndex={-1} className="focus:outline-none">
        {children}
      </main>
      <Footer />
    </>
  );
}
