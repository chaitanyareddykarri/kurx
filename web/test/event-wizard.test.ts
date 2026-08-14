import { describe, expect, it } from "vitest";
import {
  categoriesFor, cleanGroup, toIsoUtc, toLocalInput, typesFor, withUtcTimes
} from "@/lib/event-wizard";

/// D-265 — the create-event wizard's payload shaping. Both functions encode a server contract and
/// fail silently when wrong, which is exactly the kind of logic that needs a test rather than a
/// careful read.

describe("cleanGroup", () => {
  it("returns undefined when every field is blank, so an untouched step sends nothing", () => {
    // The whole point: on the server null means "leave alone", empty string means "clear". A step
    // the organiser never opened must not clear the fields another step set.
    expect(cleanGroup({ tagline: "", rules: "", faqJson: undefined })).toBeUndefined();
  });

  it("keeps only the fields that carry a value", () => {
    expect(cleanGroup({ tagline: "Ship it", shortDescription: "", rules: undefined })).toEqual({
      tagline: "Ship it"
    });
  });

  it("keeps false — an unchecked switch is a real answer, not an absent one", () => {
    expect(cleanGroup({ autoClose: false })).toEqual({ autoClose: false });
  });

  it("keeps zero — a zero fee is a deliberate waiver", () => {
    expect(cleanGroup({ platformFeePercent: 0 })).toEqual({ platformFeePercent: 0 });
  });

  it("drops NaN, which is what an empty numeric input parses to", () => {
    expect(cleanGroup({ minAge: Number("") || Number.NaN, maxAge: 25 })).toEqual({ maxAge: 25 });
  });

  it("drops null as well as undefined", () => {
    expect(cleanGroup({ a: null, b: "x" })).toEqual({ b: "x" });
  });
});

describe("toIsoUtc", () => {
  it("returns undefined for an empty input", () => {
    expect(toIsoUtc("")).toBeUndefined();
    expect(toIsoUtc(undefined)).toBeUndefined();
  });

  it("converts a zoneless datetime-local value to a UTC instant", () => {
    // `datetime-local` has no zone. Sent raw the server reads it as UTC and every time shifts by the
    // organiser's offset — in India that is 5h30m, which nobody notices until check-in.
    const iso = toIsoUtc("2026-08-10T18:30");
    expect(iso).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/);
    // Round-tripping through the local zone must land on the same wall clock the organiser typed.
    const back = new Date(iso!);
    expect(back.getHours()).toBe(18);
    expect(back.getMinutes()).toBe(30);
  });

  it("returns undefined rather than 'Invalid Date' for junk", () => {
    expect(toIsoUtc("not-a-date")).toBeUndefined();
  });
});

/// D-305 — which Types and Categories the gate's Public/Private answer permits.
///
/// This is a data rule, not a rendering one, so it is tested against the predicate rather than through
/// the DOM. Getting it wrong shows the organiser the wrong catalogue — a wizard that looks completely
/// normal and offers a Wedding under "Public" — which is the failure shape this whole session has been
/// chasing: correct-looking output over the wrong data.
describe("typesFor", () => {
  const types = [
    { id: "wedding", parent_id: "family", product_class: "Private" },
    { id: "meetup", parent_id: "community", product_class: "Public" },
    { id: "legacy", parent_id: "community", product_class: null },
    { id: "unset", parent_id: "community" }
  ];

  it("offers only Private types when Private was chosen", () => {
    expect(typesFor(types, "Private").map((t) => t.id)).toEqual(["wedding"]);
  });

  it("treats an unclassified type as Public, exactly as the server's fallback does", () => {
    // ResolveArchetypeAsync returns EventProduct.Public when ProductClass is null. If the client ever
    // disagreed, a Type would be offered here and then produce an event of the other class.
    expect(typesFor(types, "Public").map((t) => t.id)).toEqual(["meetup", "legacy", "unset"]);
  });

  it("never offers a Private type under Public", () => {
    expect(typesFor(types, "Public").some((t) => t.id === "wedding")).toBe(false);
  });
});

describe("categoriesFor", () => {
  const categories = [
    { id: "family" },
    { id: "community" },
    { id: "empty" }
  ];
  const types = [
    { id: "wedding", parent_id: "family", product_class: "Private" },
    { id: "meetup", parent_id: "community", product_class: "Public" }
  ];

  it("keeps every category for Public, including one with no types at all", () => {
    // A category with no Types still yields a Public event: Type is optional and a null Type resolves
    // to Public. Hiding it would block a legitimate event.
    expect(categoriesFor(categories, types, "Public").map((c) => c.id)).toEqual(["family", "community", "empty"]);
  });

  it("hides categories that cannot produce a Private event", () => {
    // Otherwise the organiser picks "Community", reaches the Type step, finds it empty, and has no way
    // forward — a dead end that looks like a bug in the taxonomy.
    expect(categoriesFor(categories, types, "Private").map((c) => c.id)).toEqual(["family"]);
  });
});

describe("withUtcTimes", () => {
  /// D-289 — the return leg of the round-trip. `toLocalInput` renders a stored instant in the
  /// browser's zone; without this the form posted that wall-clock string bare, the server read it as
  /// UTC, and the event moved by the organiser's offset **on every save**, so the error compounded.

  function fd(entries: Record<string, string>) {
    const f = new FormData();
    for (const [k, v] of Object.entries(entries)) f.set(k, v);
    return f;
  }

  it("converts both datetime fields to UTC instants", () => {
    const out = withUtcTimes(fd({ startsAt: "2026-09-15T10:00", endsAt: "2026-09-15T17:00" }));
    expect(out.get("startsAt")).toBe(toIsoUtc("2026-09-15T10:00"));
    expect(out.get("endsAt")).toBe(toIsoUtc("2026-09-15T17:00"));
  });

  it("is idempotent, which is what stops the shift compounding across saves", () => {
    // The bug was not that one save was wrong — it was that saving again moved it again. Feeding a
    // value that has already been converted must be a no-op.
    const once = withUtcTimes(fd({ startsAt: "2026-09-15T10:00" }));
    const twice = withUtcTimes(once);
    expect(twice.get("startsAt")).toBe(once.get("startsAt"));
  });

  it("preserves the wall clock the organiser typed", () => {
    const out = withUtcTimes(fd({ startsAt: "2026-09-15T10:00" }));
    const back = new Date(String(out.get("startsAt")));
    expect(back.getHours()).toBe(10);
    expect(back.getMinutes()).toBe(0);
  });

  it("leaves a blank field blank — absent means 'leave alone', not 'set to now'", () => {
    const out = withUtcTimes(fd({ startsAt: "", endsAt: "2026-09-15T17:00" }));
    expect(out.get("startsAt")).toBe("");
    expect(out.get("endsAt")).toBe(toIsoUtc("2026-09-15T17:00"));
  });

  it("leaves an unparseable value untouched rather than inventing one", () => {
    const out = withUtcTimes(fd({ startsAt: "not-a-date" }));
    expect(out.get("startsAt")).toBe("not-a-date");
  });

  it("only touches the fields it is given", () => {
    const out = withUtcTimes(fd({ title: "Workshop", startsAt: "2026-09-15T10:00" }));
    expect(out.get("title")).toBe("Workshop");
  });
});

describe("toLocalInput", () => {
  /// The display leg. One of the three copies this replaced was `iso.slice(0, 16)`, which puts the
  /// UTC wall clock into a control the browser reads as local — invisible until something converts
  /// on the way back, at which point it shifts the value the other way (D-289).

  it("round-trips with toIsoUtc, which is the only property that matters", () => {
    const typed = "2026-09-15T10:00";
    expect(toLocalInput(toIsoUtc(typed)!)).toBe(typed);
  });

  it("renders a UTC instant in the browser's zone, not by truncating the string", () => {
    const iso = "2026-09-15T04:30:00Z";
    const expected = new Date(iso);
    const [, hh, mm] = toLocalInput(iso).match(/T(\d{2}):(\d{2})$/)!;
    expect(Number(hh)).toBe(expected.getHours());
    expect(Number(mm)).toBe(expected.getMinutes());
  });

  it("returns an empty string for junk rather than 'NaN-NaN-NaN'", () => {
    expect(toLocalInput("not-a-date")).toBe("");
  });
});
