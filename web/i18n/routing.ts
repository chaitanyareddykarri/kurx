import { defineRouting } from "next-intl/routing";

export const routing = defineRouting({
  locales: ["en", "hi"] as const,
  defaultLocale: "en",
  // No /en/ or /hi/ URL prefixes — locale is stored in a cookie and detected
  // from the Accept-Language header, keeping existing URLs unchanged.
  localePrefix: "never"
});

export type Locale = (typeof routing.locales)[number];
