"use client";

import { useMemo, useRef, useState, useId } from "react";
import {
  AsYouType,
  getCountries,
  getCountryCallingCode,
  parsePhoneNumberFromString,
  type CountryCode
} from "libphonenumber-js";

/**
 * Shared phone input for web + admin (Phase 6, ADR-A6 / D-089). One canonical component so every
 * authentication surface produces the same thing: an **E.164** value (`+919876543210`). Uses
 * `libphonenumber-js` — the same library family as the backend's `PhoneCanonicalizer` — for parsing,
 * validation, as-you-type national formatting and the country list, so client and server agree on what
 * is a valid number. The backend remains the authoritative validator; this is fail-fast UX.
 *
 * Country names come from `Intl.DisplayNames`, flags are derived from the ISO code (regional-indicator
 * code points), so there is no hand-maintained country table to drift.
 */

const regionNames =
  typeof Intl !== "undefined" && "DisplayNames" in Intl ? new Intl.DisplayNames(["en"], { type: "region" }) : null;

function countryName(iso: string): string {
  return regionNames?.of(iso) ?? iso;
}

/** ISO 3166-1 alpha-2 → flag emoji via regional-indicator code points. */
function flagEmoji(iso: string): string {
  return iso
    .toUpperCase()
    .replace(/./g, (c) => String.fromCodePoint(0x1f1e6 + c.charCodeAt(0) - 65));
}

export type PhoneFieldProps = {
  /** Current E.164 value (or ""). */
  value: string;
  /** Emits the E.164 string and whether libphonenumber considers it a valid, complete number. */
  onChange: (e164: string, valid: boolean) => void;
  defaultCountry?: CountryCode;
  id?: string;
  label?: string;
  error?: string;
  disabled?: boolean;
  autoFocus?: boolean;
  onEnter?: () => void;
};

const inputCls =
  "h-11 w-full rounded-r-md border border-l-0 border-border-strong bg-background px-3 text-sm text-text placeholder:text-muted focus:border-accent focus:outline-none disabled:opacity-50";

export function PhoneField({
  value,
  onChange,
  defaultCountry = "IN",
  id: idProp,
  label,
  error,
  disabled,
  autoFocus,
  onEnter
}: PhoneFieldProps) {
  /*
   * A `label` with no `id` used to render a `<label htmlFor={undefined}>` beside an
   * `<input id={undefined}>` — a heading-shaped string that named nothing, so the control's
   * accessible name fell back to its placeholder, which disappears as soon as anyone types. It
   * failed silently and looked correct on screen, and two of the four call sites hit it: the
   * signup step and Settings › phone change.
   *
   * `useId` is what `Field` in this same package already does, so a caller now gets the wiring for
   * free and `id` stays available for the cases that need to name the control from outside.
   */
  const fallbackId = useId();
  const id = idProp ?? fallbackId;
  const initial = useMemo(() => {
    const parsed = value ? parsePhoneNumberFromString(value) : undefined;
    if (parsed?.country) {
      return { country: parsed.country, national: new AsYouType(parsed.country).input(parsed.nationalNumber) };
    }
    return { country: defaultCountry, national: "" };
    // deliberately once — the field owns its display state after mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const [country, setCountry] = useState<CountryCode>(initial.country);
  const [national, setNational] = useState(initial.national);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const searchRef = useRef<HTMLInputElement>(null);

  const countries = useMemo(
    () =>
      getCountries()
        .map((iso) => ({ iso, name: countryName(iso), code: getCountryCallingCode(iso) }))
        .sort((a, b) => a.name.localeCompare(b.name)),
    []
  );

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return countries;
    const noPlus = q.replace("+", "");
    return countries.filter(
      (c) => c.name.toLowerCase().includes(q) || c.iso.toLowerCase() === q || c.code.startsWith(noPlus)
    );
  }, [search, countries]);

  function emit(iso: CountryCode, nat: string) {
    const digits = nat.replace(/\D/g, "");
    if (!digits) {
      onChange("", false);
      return;
    }
    const parsed = parsePhoneNumberFromString(digits, iso);
    if (parsed) {
      onChange(parsed.number, parsed.isValid());
    } else {
      onChange(`+${getCountryCallingCode(iso)}${digits}`, false);
    }
  }

  function onNationalChange(raw: string) {
    const trimmed = raw.trim();
    // Pasting a full international number re-selects the country and reformats.
    if (trimmed.startsWith("+")) {
      const parsed = parsePhoneNumberFromString(trimmed);
      if (parsed?.country) {
        setCountry(parsed.country);
        setNational(new AsYouType(parsed.country).input(parsed.nationalNumber));
        onChange(parsed.number, parsed.isValid());
        return;
      }
    }
    const formatted = new AsYouType(country).input(trimmed);
    setNational(formatted);
    emit(country, formatted);
  }

  function pick(iso: CountryCode) {
    setCountry(iso);
    setOpen(false);
    setSearch("");
    const digits = national.replace(/\D/g, "");
    const reformatted = new AsYouType(iso).input(digits);
    setNational(reformatted);
    emit(iso, reformatted);
  }

  return (
    <div className="space-y-1.5">
      {label ? (
        <label htmlFor={id} className="block text-xs font-bold text-text">
          {label}
        </label>
      ) : null}
      <div className="relative flex">
        <button
          type="button"
          disabled={disabled}
          onClick={() => {
            setOpen((o) => !o);
            setTimeout(() => searchRef.current?.focus(), 0);
          }}
          className="flex h-11 shrink-0 items-center gap-1.5 rounded-l-md border border-border-strong bg-surface px-2.5 text-sm text-text hover:bg-elevated focus:border-accent focus:outline-none disabled:opacity-50"
          aria-label={`Country: ${countryName(country)} (+${getCountryCallingCode(country)})`}
          aria-expanded={open}
        >
          <span aria-hidden className="text-base leading-none">
            {flagEmoji(country)}
          </span>
          <span className="text-muted">+{getCountryCallingCode(country)}</span>
          <span aria-hidden className="text-muted">
            ▾
          </span>
        </button>
        <input
          id={id}
          type="tel"
          inputMode="tel"
          autoComplete="tel-national"
          autoFocus={autoFocus}
          disabled={disabled}
          value={national}
          onChange={(event) => onNationalChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && onEnter) onEnter();
          }}
          placeholder="Phone number"
          aria-invalid={Boolean(error)}
          className={inputCls}
        />

        {open ? (
          <div className="absolute left-0 top-12 z-20 max-h-72 w-72 overflow-auto rounded-md border border-border bg-surface shadow-github">
            <div className="sticky top-0 border-b border-border bg-surface p-2">
              <input
                ref={searchRef}
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search country or code"
                aria-label="Search country"
                className="h-9 w-full rounded-md border border-border-strong bg-background px-2.5 text-sm text-text placeholder:text-muted focus:border-accent focus:outline-none"
              />
            </div>
            <ul role="listbox">
              {filtered.map((c) => (
                <li key={c.iso}>
                  <button
                    type="button"
                    onClick={() => pick(c.iso)}
                    className={`flex w-full items-center gap-2.5 px-3 py-2 text-left text-sm hover:bg-elevated ${
                      c.iso === country ? "bg-elevated" : ""
                    }`}
                  >
                    <span aria-hidden className="text-base leading-none">
                      {flagEmoji(c.iso)}
                    </span>
                    <span className="flex-1 truncate text-text">{c.name}</span>
                    <span className="text-muted">+{c.code}</span>
                  </button>
                </li>
              ))}
              {filtered.length === 0 ? <li className="px-3 py-3 text-sm text-muted">No matches.</li> : null}
            </ul>
          </div>
        ) : null}
      </div>
      {error ? <p className="text-xs text-danger">{error}</p> : null}
    </div>
  );
}
