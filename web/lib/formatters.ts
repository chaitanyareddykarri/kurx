/**
 * Currency formatter — converts paise (backend unit) to a locale-formatted string.
 * Default locale is en-IN (Indian English) and currency is INR.
 *
 * Usage:
 *   formatCurrency(50000)          // "₹500"
 *   formatCurrency(50000, "hi-IN") // "₹500" (Devanagari digits in some browsers)
 *   formatCurrency(50000, "en-IN", "USD") // "$500"
 */
export function formatCurrency(
  paise: number,
  locale: string = "en-IN",
  currency: string = "INR"
): string {
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    maximumFractionDigits: 0
  }).format(paise / 100);
}

/**
 * Date formatter.
 *
 * Usage:
 *   formatDate("2026-01-15")                         // "15 January 2026"
 *   formatDate("2026-01-15", "hi-IN")                // "१५ जनवरी २०२६"
 *   formatDate("2026-01-15", "en-IN", { month: "short" }) // "15 Jan 2026"
 */
export function formatDate(
  date: Date | string,
  locale: string = "en-IN",
  options: Intl.DateTimeFormatOptions = {
    day: "numeric",
    month: "long",
    year: "numeric"
  }
): string {
  return new Intl.DateTimeFormat(locale, options).format(new Date(date));
}

/**
 * Date + time formatter.
 *
 * Usage:
 *   formatDateTime("2026-01-15T18:30:00Z")          // "15 January 2026, 12:00 AM"
 */
export function formatDateTime(
  date: Date | string,
  locale: string = "en-IN"
): string {
  return new Intl.DateTimeFormat(locale, {
    day: "numeric",
    month: "long",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(new Date(date));
}

/**
 * Renders the public profile's month-precision join date (`"2026-08"`) as "August 2026".
 *
 * Pinned to UTC deliberately. `new Date("2026-08")` is midnight UTC on the 1st, so formatting it in
 * the viewer's zone would render "July 2026" for anyone west of Greenwich — the same profile saying
 * two different things depending on who is looking. A year-month is a label, not an instant, so it is
 * never localised.
 *
 * Usage:
 *   formatJoinedMonth("2026-08")   // "August 2026"
 */
export function formatJoinedMonth(yearMonth: string, locale: string = "en-IN"): string {
  const parsed = new Date(`${yearMonth}-01T00:00:00Z`);
  if (Number.isNaN(parsed.getTime())) return "";
  return new Intl.DateTimeFormat(locale, {
    month: "long",
    year: "numeric",
    timeZone: "UTC"
  }).format(parsed);
}

/**
 * Relative time formatter (e.g. "in 2 days", "3 hours ago").
 *
 * Usage:
 *   formatRelativeTime(futureDate)   // "in 2 days"
 *   formatRelativeTime(pastDate, "hi-IN") // "२ दिन पहले"
 */
export function formatRelativeTime(
  date: Date | string,
  locale: string = "en-IN"
): string {
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  const diffSeconds = (new Date(date).getTime() - Date.now()) / 1000;

  if (Math.abs(diffSeconds) < 60) return rtf.format(Math.round(diffSeconds), "second");
  if (Math.abs(diffSeconds) < 3600) return rtf.format(Math.round(diffSeconds / 60), "minute");
  if (Math.abs(diffSeconds) < 86400) return rtf.format(Math.round(diffSeconds / 3600), "hour");
  if (Math.abs(diffSeconds) < 86400 * 30) return rtf.format(Math.round(diffSeconds / 86400), "day");
  if (Math.abs(diffSeconds) < 86400 * 365) return rtf.format(Math.round(diffSeconds / (86400 * 30)), "month");
  return rtf.format(Math.round(diffSeconds / (86400 * 365)), "year");
}

/** Map of supported locale codes to their display names. */
export const SUPPORTED_LOCALES: Record<string, string> = {
  en: "English",
  hi: "हिन्दी"
};
