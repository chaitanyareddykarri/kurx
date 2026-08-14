/// Payload shaping for the create-event wizard (D-265).
///
/// Extracted from the wizard component so it can be tested without a DOM: both functions encode a
/// server contract, and getting either wrong corrupts data silently rather than failing loudly.

/// Drops empty strings, `undefined` and `NaN`, returning `undefined` when nothing survives.
///
/// This is not tidiness. On the server a **null field means "leave alone"** and an **empty string
/// means "clear"**, so a wizard step the organiser never opened must send *nothing* — sending its
/// blanks would wipe fields set in a different step. `undefined` groups vanish under
/// `JSON.stringify`, which is exactly the "untouched" the server expects.
///
/// `false` and `0` are deliberately kept: an unchecked switch and a zero fee are real values.
export function cleanGroup<T extends Record<string, unknown>>(group: T): T | undefined {
  const kept = Object.entries(group).filter(
    ([, v]) => v !== "" && v !== undefined && v !== null && !(typeof v === "number" && Number.isNaN(v))
  );
  return kept.length > 0 ? (Object.fromEntries(kept) as T) : undefined;
}

/// Converts an `<input type="datetime-local">` value to ISO-8601 UTC.
///
/// The input yields a wall-clock string with no zone (`2026-08-10T18:30`). Sent raw, the server
/// reads it as UTC and every time silently shifts by the organiser's offset — in India that is a
/// five-and-a-half hour error nobody notices until check-in. Passing it through `Date` applies the
/// browser's zone, which is the zone the organiser typed in.
export function toIsoUtc(local: string | undefined): string | undefined {
  if (!local) return undefined;
  const parsed = new Date(local);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}

/// Types the chosen product class permits (D-305).
///
/// **Absent `product_class` means Public** — the same fallback `ResolveArchetypeAsync` applies
/// server-side, so an admin-created Type that predates classification behaves identically on both
/// sides. The gate filters the catalogue; `Event.Product` is still derived and snapshotted from the
/// chosen Type at create (D-266 M1), so this can narrow what is offered but can never contradict what
/// the server will store.
///
/// Lives here rather than in the component for the reason this file exists: it encodes a server-side
/// rule, and a wrong answer shows the organiser the wrong catalogue rather than failing loudly.
/// Flutter applies the same predicate as `EventCategory.allowsProduct`.
export function typesFor<T extends { product_class?: string | null }>(
  subcategories: T[],
  product: "Public" | "Private"
): T[] {
  return subcategories.filter((s) =>
    product === "Private" ? s.product_class === "Private" : s.product_class !== "Private");
}

/// Categories worth offering for this product class.
///
/// For **Private**, a category must have at least one Private Type — otherwise it is selectable and
/// then shows an empty Type step with no way forward. **Public** keeps every category, because a
/// category with no Types at all still yields a Public event: Type is optional and a null Type resolves
/// to Public.
export function categoriesFor<T extends { id: string }, S extends { parent_id: string | null; product_class?: string | null }>(
  categories: T[],
  subcategories: S[],
  product: "Public" | "Private"
): T[] {
  if (product !== "Private") return categories;
  const allowed = typesFor(subcategories, product);
  return categories.filter((c) => allowed.some((t) => t.parent_id === c.id));
}

/// Renders a stored UTC instant as an `<input type="datetime-local">` value in the browser's zone.
///
/// `toIsoUtc`'s inverse, and the reason it has to live beside it: the two are a pair, and a form is
/// only lossless when both legs agree on a zone. Three copies of this existed, and one of them —
/// `dtLocal` on the ticket-type edit form — was `iso.slice(0, 16)`, which puts the **UTC** wall clock
/// into a control the browser reads as local time (D-289). That truncation is invisible until
/// something converts on the way back, at which point it shifts the value in the opposite direction.
export function toLocalInput(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/// Applies `toIsoUtc` to named `datetime-local` fields of a `FormData` before a server action reads it.
///
/// The forms that post through a server action had no equivalent of the wizard's conversion, so they
/// sent the wall-clock string bare and the server read it as UTC. Because those same forms render the
/// stored instant back through a browser-local formatter, the error **compounded**: an event typed at
/// 10:00 was stored at 15:30, and one save that changed nothing moved it to 21:00 (D-289). A form is
/// only lossless when both directions agree on the zone, and this is the return leg.
///
/// A blank field is left untouched: on these endpoints a missing field means "leave alone", so
/// inventing a timestamp here would overwrite a good one with a guess.
export function withUtcTimes(formData: FormData, fields: readonly string[] = ["startsAt", "endsAt"]): FormData {
  for (const field of fields) {
    const local = formData.get(field);
    if (typeof local !== "string" || local === "") continue;
    const iso = toIsoUtc(local);
    if (iso) formData.set(field, iso);
  }
  return formData;
}
