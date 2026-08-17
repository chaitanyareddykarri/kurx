/**
 * The vocabulary of things that change from one certificate to the next (D-355).
 *
 * One canonical list, used by everything that needs to know what a dynamic field *is*: the Add Field
 * menu, the grouping in the side panel, the sample text the canvas draws, and — when detection arrives —
 * the mapping from whatever words a designer happened to print onto the page.
 *
 * The `synonyms` are the interesting part. A certificate might say "Recipient's Full Name", "Student
 * Name", "Learner", or just carry an example like "John Doe". Those are the same field wearing different
 * clothes, and a creator should never have to reconcile them by hand. Matching is on letters and digits
 * only, so spacing, punctuation and case are all irrelevant.
 */

export type CanonicalFieldKey =
  | "participant_name" | "event_name" | "organization" | "issue_date" | "expiry_date"
  | "certificate_id" | "instructor_name" | "organizer_name" | "location" | "hours"
  | "score" | "grade";

export interface CanonicalField {
  key: CanonicalFieldKey;
  /** What the creator sees. Sentence case, no jargon. */
  label: string;
  /** Drawn on the canvas so the design reads like a finished certificate rather than a form. */
  sample: string;
  /** Other ways a design might name or exemplify this field. Normalised on comparison. */
  synonyms: string[];
}

export const CANONICAL_FIELDS: CanonicalField[] = [
  {
    key: "participant_name",
    label: "Participant name",
    sample: "Ananya Rao",
    synonyms: [
      "name", "full name", "participant", "participant name", "recipient", "recipient name",
      "recipient's full name", "student", "student name", "learner",
      "learner name", "candidate", "candidate name", "attendee", "attendee name",
      "john doe", "jane doe",
    ],
  },
  {
    key: "event_name",
    label: "Event name",
    sample: "Confined Space Entry Training",
    synonyms: [
      "event", "event name", "course", "course name", "training", "training name", "program",
      "programme", "program name", "workshop", "workshop name", "session", "activity", "subject",
    ],
  },
  {
    key: "organization",
    label: "Organisation",
    sample: "Example Institute",
    synonyms: [
      "organization", "organisation", "company", "company name", "your company name", "institution",
      "institute", "college", "university", "school", "academy", "issued by",
    ],
  },
  {
    key: "issue_date",
    label: "Issue date",
    sample: "15 August 2026",
    synonyms: [
      "date", "issue date", "date of issue", "date of issuance", "issued", "issued on",
      "completion date", "date completed", "certificate date", "awarded on",
    ],
  },
  {
    key: "expiry_date",
    label: "Expiry date",
    sample: "15 August 2029",
    synonyms: [
      "expiry", "expiry date", "expiration", "expiration date", "expires", "expires on",
      "valid until", "valid till", "date of validity", "validity", "renewal date",
    ],
  },
  {
    key: "certificate_id",
    label: "Certificate ID",
    sample: "CERT-2026-00042",
    synonyms: [
      "certificate id", "certificate no", "certificate number", "cert no", "cert id",
      "credential id", "serial", "serial no", "serial number", "reference", "reference no",
    ],
  },
  {
    key: "instructor_name",
    label: "Instructor name",
    sample: "Dr. Meera Iyer",
    synonyms: [
      "instructor", "instructor name", "trainer", "trainer name", "teacher", "faculty",
      "facilitator", "coach", "mentor",
    ],
  },
  {
    key: "organizer_name",
    label: "Organiser name",
    sample: "Kurx Events",
    synonyms: [
      "organizer", "organiser", "organizer name", "organiser name", "host", "hosted by",
      "conducted by", "presented by",
    ],
  },
  {
    key: "location",
    label: "Location",
    sample: "Hyderabad",
    synonyms: ["location", "venue", "place", "city", "held at", "venue name"],
  },
  {
    key: "hours",
    label: "Hours",
    sample: "8",
    synonyms: ["hours", "duration", "contact hours", "cpd hours", "credit hours", "hrs"],
  },
  {
    key: "score",
    label: "Score",
    sample: "92%",
    // "points" is deliberately absent. It is far more often decoration than a label — a CPD badge reading
    // "8 CPD / POINTS" had its badge turned into a score field, covering the artwork in the badge's own
    // purple and printing a value into it. Per this file's rule, missing a real "Points:" label costs a
    // menu click; a confident mistake costs every certificate in the run.
    synonyms: ["score", "marks", "result", "percentage"],
  },
  {
    key: "grade",
    label: "Grade",
    sample: "Distinction",
    synonyms: ["grade", "class", "division", "band", "level achieved"],
  },
];

const BY_KEY = new Map(CANONICAL_FIELDS.map((f) => [f.key as string, f]));

export function canonicalField(key: string | null | undefined): CanonicalField | null {
  return key ? BY_KEY.get(key) ?? null : null;
}

/** How a field is named to the creator. An unrecognised key is a custom field and keeps its own name. */
export function fieldLabel(key: string | null | undefined, fallback?: string | null): string {
  return canonicalField(key)?.label ?? fallback ?? prettify(key) ?? "Text";
}

/** Sample text for the canvas, so a design under construction reads like a finished certificate. */
export function fieldSample(key: string | null | undefined, fallback?: string | null): string {
  return canonicalField(key)?.sample ?? fallback ?? prettify(key) ?? "Text";
}

/** Letters and digits only, lowercased — so "Recipient's Full Name", "recipient_full_name" and
 *  "RECIPIENT FULL NAME" are one string. */
export function normalise(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, "");
}

/**
 * Which canonical field a piece of text is most likely to be, or null.
 *
 * Exact synonym match only — no fuzzy scoring. A wrong guess here silently prints the wrong words on
 * every certificate in a run, so the cost of missing one (the creator picks it from a menu) is far lower
 * than the cost of a confident mistake.
 */
export function matchCanonicalField(text: string): CanonicalFieldKey | null {
  const needle = normalise(text);
  if (!needle) return null;

  for (const field of CANONICAL_FIELDS) {
    if (normalise(field.key) === needle) return field.key;
    if (normalise(field.label) === needle) return field.key;
    for (const synonym of field.synonyms) {
      if (normalise(synonym) === needle) return field.key;
    }
  }
  return null;
}

/** Turns `some_custom_thing` into `Some custom thing` for a field nobody catalogued. */
function prettify(key: string | null | undefined): string | null {
  if (!key) return null;
  const words = key.replace(/[_-]+/g, " ").trim();
  return words ? words.charAt(0).toUpperCase() + words.slice(1) : null;
}


/** A creator types "Cohort Number"; the pipeline needs `cohort_number`. */
export function toKey(name: string): string {
  return name.trim().toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
}


/**
 * Phrases that announce the recipient rather than being it.
 *
 * These were once synonyms for the name itself, which read a certificate exactly backwards. A design says
 *
 *     Presented to
 *     OLIVER SMITH
 *
 * and treating the lead-in as the name replaced the words "Presented to" with the recipient's name, while
 * "OLIVER SMITH" stayed printed underneath as fixed text — so every certificate in the run carried one
 * person's name twice and lost its own label.
 *
 * What the phrase actually tells us is where the name *is*: on the next line. That is the signal, and it
 * is what {@link leadsIntoName} exists for.
 */
const NAME_LEAD_INS = [
  "presented to", "awarded to", "this is to certify that", "this certifies that",
  "is hereby awarded to", "proudly presented to", "certificate presented to", "this is presented to",
  "hereby awarded to", "in recognition of",
];

/** Whether this text announces a name that follows it. */
export function leadsIntoName(text: string): boolean {
  const needle = normalise(text);
  return needle.length > 0 && NAME_LEAD_INS.some((phrase) => normalise(phrase) === needle);
}
