import { describe, expect, it, vi } from "vitest";

// `lib/api` calls React's `cache()` at module scope, and `cache` only exists under the react-server
// export condition — in jsdom it is undefined and the import throws "cache is not a function",
// which vitest reports as a suite that collected 0 tests rather than a failure (see test/setup.ts,
// which makes the same note for the chat suites). The schemas under test are plain zod objects; this
// only makes the module importable. Hoisted by vitest, so it applies before the import below.
vi.mock("react", async () => {
  const actual = await vi.importActual<typeof import("react")>("react");
  return { ...actual, cache: (fn: unknown) => fn };
});

import {
  attendanceSchema, salesPointSchema, ticketTypeRevenueSchema, attendeeSchema, publicProfileSchema
} from "@/lib/api";

/// D-289 — the schemas that broke because they disagreed with the wire, and the two rules that keep
/// them honest.
///
/// **Rule 1: responses are snake_case.** `SnakeCaseResponseConverter` (D-259 addendum) renames every
/// property of every type in `Kurx.Application.Abstractions` on the way out, matching by namespace
/// rather than by a name suffix. Four schemas here were written camelCase and threw on contact with
/// a real response.
///
/// **Rule 2: a nullable column is nullable on the wire.** `users.Skills` is a nullable array and the
/// profile projection passes it straight through, so `null` — not `[]` — is what an untouched field
/// looks like.
///
/// These assert against payloads copied from live responses, because a schema that agrees only with
/// itself is what produced the outage: two of the four broke silently, since an empty list parses
/// clean and the page renders "0 attendees" right up until the first real row arrives.

describe("analytics schemas parse the wire shape", () => {
  it("attendance — the payload that took the analytics page down", () => {
    // Verbatim from GET /v1/orgs/{org}/events/{id}/analytics/attendance.
    const parsed = attendanceSchema.parse({ total_tickets: 0, checked_in: 0, attendance_rate: 0 });
    expect(parsed.total_tickets).toBe(0);
    expect(parsed.attendance_rate).toBe(0);
  });

  it("attendance — rejects the camelCase it used to expect, so the old shape cannot creep back", () => {
    expect(() => attendanceSchema.parse({ totalTickets: 0, checkedIn: 0, attendanceRate: 0 })).toThrow();
  });

  it("sales timeline", () => {
    const parsed = salesPointSchema.parse({ date: "2026-08-08T00:00:00Z", ticket_count: 0, revenue_paise: 0 });
    expect(parsed.ticket_count).toBe(0);
  });

  it("revenue by ticket type", () => {
    const parsed = ticketTypeRevenueSchema.parse({
      ticket_type_id: "a7fd38b0-cc7c-4b26-ad4e-3c503ea416f8", name: "General",
      quantity_sold: 3, revenue_paise: 15000
    });
    expect(parsed.quantity_sold).toBe(3);
  });
});

describe("attendee roster parses the wire shape", () => {
  /// The latent one: this schema's break is invisible while the roster is empty, which is every
  /// event until the moment the page starts to matter.
  const row = {
    ticket_id: "3f1a…", code: "B9EE8G", state: "Issued", checked_in_at: null,
    buyer_name: "Audit Tester", buyer_phone: "919000000007",
    ticket_type_id: "a7fd38b0…", ticket_type_name: "General",
    group_id: null, group_number: null, group_display_name: null,
    answers: {}, buyer_user_id: "4c555324…", buyer_username: "audittester47", buyer_avatar_key: null
  };

  it("parses a populated row", () => {
    expect(attendeeSchema.parse(row).buyer_name).toBe("Audit Tester");
  });

  it("rejects the camelCase shape", () => {
    expect(() => attendeeSchema.parse({ ...row, ticket_id: undefined, ticketId: "3f1a…" })).toThrow();
  });
});

describe("public profile tolerates the nullable columns it is fed", () => {
  const base = {
    id: "109b280e…", name: "Dev Admin", username: "dev_admin", headline: null, bio: null,
    summary: "Dev Admin is building their verified event identity on Kurx.",
    avatar_key: null, cover_key: null, college: null, links_json: null,
    stats: {
      events_conducted: 0, events_attended: null, certificates_count: 0,
      participations: 0, achievements: 0, ally_count: 0
    },
    verification: {
      identity_verified: false, verified_member: false, organizer: false,
      verified_certificates: 0, years_on_platform: 0
    },
    event_dna: [], achievements: [], identity_labels: [], derived_headline: "", organizations: []
  };

  it("normalises skills: null to an empty array — the shape every new account has", () => {
    // This exact payload returned HTTP 500 for every user who had never added a skill, which is all
    // of them on the day they sign up.
    const parsed = publicProfileSchema.parse({ ...base, skills: null });
    expect(parsed.skills).toEqual([]);
  });

  it("still parses a populated skills array", () => {
    expect(publicProfileSchema.parse({ ...base, skills: ["Testing"] }).skills).toEqual(["Testing"]);
  });
});
