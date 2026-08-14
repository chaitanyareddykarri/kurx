/**
 * Display-edge formatting shared across the admin console.
 *
 * `money` existed as three separate copies (events-table, event-workspace-sheet, org-detail-content)
 * and they had drifted: two accepted a currency, one hardcoded ₹. That third copy rendered a non-INR
 * org's balances as rupees — a wrong number, not a cosmetic difference. One definition, one behaviour
 * (D-257).
 */

/** Rendered symbol per ISO-4217 code; an unmapped code falls back to the code itself ("AUD 1,200"),
 *  because a plainly-labelled amount is recoverable and a confidently wrong symbol is not. */
const SYMBOLS: Record<string, string> = {
  INR: "₹",
  USD: "$",
  EUR: "€",
  GBP: "£",
  AED: "AED ",
  SGD: "S$"
};

/**
 * `minor` is the amount in the currency's minor unit (paise for INR, cents for USD).
 *
 * `currency` defaults to INR so a payload that does not carry one renders exactly as before; pass it
 * wherever the server sends it.
 */
export function money(minor: number, currency = "INR"): string {
  const code = currency.trim().toUpperCase();
  const symbol = SYMBOLS[code] ?? `${code} `;
  return `${symbol}${(minor / 100).toLocaleString("en-IN", { maximumFractionDigits: 0 })}`;
}
