"use client";

import { useLocale } from "next-intl";
import { useRouter, usePathname } from "next/navigation";
import { useTransition } from "react";
import { SUPPORTED_LOCALES } from "@/lib/formatters";
import { setLanguageAction } from "@/lib/account-actions";

export function LanguageSwitcher() {
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const [isPending, startTransition] = useTransition();

  const nextLocale = locale === "en" ? "hi" : "en";

  function handleSwitch() {
    // The cookie is what the middleware reads on the very next request, so the UI flips immediately.
    document.cookie = `NEXT_LOCALE=${nextLocale};path=/;max-age=31536000;SameSite=Lax`;
    startTransition(async () => {
      // …and the choice is also persisted on the account (D-263), so it follows the user to another
      // browser or to the app. Cookie-only would have made `User.Language` a column with no writer.
      // Best-effort: a signed-out visitor has nothing to persist to, and a failure here must not
      // undo a language switch that has already visibly happened.
      await setLanguageAction(nextLocale).catch(() => {});
      router.refresh();
    });
  }

  return (
    <button
      onClick={handleSwitch}
      disabled={isPending}
      aria-label={`Switch to ${SUPPORTED_LOCALES[nextLocale]}`}
      // Was 36px tall — under the 44px touch floor, on the only control that reaches Hindi.
      className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-md px-2.5 text-sm font-medium text-muted transition-colors hover:bg-surface hover:text-text focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-50"
    >
      {SUPPORTED_LOCALES[nextLocale]}
    </button>
  );
}
