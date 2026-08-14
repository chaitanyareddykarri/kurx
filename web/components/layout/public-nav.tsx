import Link from "next/link";
import { Download, LogIn } from "lucide-react";
import { KurxLogo } from "@/components/brand/logo";
import { LinkButton } from "@/components/ui/button";
import { publicPages } from "@/lib/site";
import { ThemeToggle } from "@/components/layout/theme-toggle";
import { LanguageSwitcher } from "@/components/layout/language-switcher";
import { getTranslations } from "next-intl/server";

export async function PublicNav() {
  const t = await getTranslations("nav");

  const labelMap: Record<string, string> = {
    "/features": t("features"),
    "/pricing": t("pricing"),
    "/about": t("about"),
    "/support": t("support"),
    "/contact": t("contact")
  };

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-background/88 backdrop-blur">
      <nav className="container-shell flex h-16 items-center justify-between gap-4">
        {/* The logo link was 28px tall — a touch target below the 44px floor. */}
        <Link href="/" aria-label="Kurx home" className="inline-flex min-h-11 items-center rounded-md focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
          <KurxLogo />
        </Link>
        {/*
          `lg`, not `md`. At 768 this row is 379px and the actions 387px beside a 71px logo — 869px
          of content in a 736px container, so every public page scrolled sideways at exactly the
          tablet width. It first fits at 1024. Between 768 and 1023 these destinations stay reachable
          from the footer, which is why Phase 11 added one.
        */}
        <div className="hidden items-center gap-1 lg:flex">
          {publicPages.slice(1, 6).map((page) => (
            <Link key={page.href} href={page.href} className="rounded-md px-3 py-2 text-sm text-muted hover:bg-surface hover:text-text">
              {labelMap[page.href] ?? page.label}
            </Link>
          ))}
        </div>
        {/*
          At 360px this row was 284px beside a 71px logo inside a 328px container — every public page
          scrolled sideways. Two of its controls were also under the 44px touch floor.

          Rather than dropping a control, the primary CTA goes icon-only below `sm` and the gaps
          tighten. Nothing becomes unreachable: every control keeps a 44px target and an accessible
          name, and the label returns as soon as there is room for it.
        */}
        <div className="flex items-center gap-1 sm:gap-2">
          <LanguageSwitcher />
          <ThemeToggle />
          {/*
            Both were routes to a form that no longer sits on `/`: "Log in" was the `#login`
            anchor into the home page's sign-in column, and "Continue" went to the auth-gated
            `/discover`, which bounced signed-out visitors back to that anchor. Both now go
            straight to `/login`, which forwards anyone already signed in (D-290).
          */}
          <LinkButton href="/login" variant="secondary">
            <LogIn size={16} aria-hidden /> {t("login")}
          </LinkButton>
          <LinkButton href="/login" variant="secondary" className="hidden sm:inline-flex">
            {t("discover")}
          </LinkButton>
          <LinkButton href="#download-app" aria-label={t("getApp")}>
            <Download size={16} aria-hidden />
            <span className="hidden sm:inline">{t("getApp")}</span>
          </LinkButton>
        </div>
      </nav>
    </header>
  );
}
