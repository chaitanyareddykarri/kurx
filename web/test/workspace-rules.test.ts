import { describe, expect, it } from "vitest";
import { awaitingReview, hostWorkspaceOpen, OPEN_HOST_STATES } from "@/lib/workspace";

/// D-305 — User Workspace state rules.
///
/// The one decision this page makes is **whether opening an event leads to its Event Host Workspace or
/// to a status card**. Getting it wrong in either direction is a real failure: too permissive lets an
/// organiser into a management surface for an event nobody approved; too strict tells them to wait for
/// an approval that already happened.
///
/// These sets must stay in step with the Flutter hub's identically-named ones. Two clients disagreeing
/// here is the same bug twice, and neither would look broken.

describe("hostWorkspaceOpen", () => {
  it("opens only after the event has been approved and published onward", () => {
    for (const s of ["published", "scheduled", "live", "completed", "closed"]) {
      expect(hostWorkspaceOpen(s)).toBe(true);
    }
  });

  it("stays shut for everything before approval", () => {
    // A draft or an event under review is not a working workspace. Presenting one would be the same
    // lie as a decorative QR: a surface that looks operational over nothing.
    for (const s of ["draft", "pendingreview", "underreview", "approved", "rejected", "changesrequested"]) {
      expect(hostWorkspaceOpen(s)).toBe(false);
    }
  });

  it("stays shut for cancelled and archived events", () => {
    expect(hostWorkspaceOpen("cancelled")).toBe(false);
    expect(hostWorkspaceOpen("archived")).toBe(false);
  });

  it("is case-insensitive, because the API sends TitleCase and the set is lowercase", () => {
    // `EventStatus.ToString()` yields "Published"; a comparison that forgot to lower-case it would
    // close every host workspace on the platform and look like an approval bug.
    expect(hostWorkspaceOpen("Published")).toBe(true);
    expect(hostWorkspaceOpen("PENDINGREVIEW")).toBe(false);
  });
});

describe("awaitingReview", () => {
  it("counts only the states where the platform holds the decision", () => {
    expect(awaitingReview("pendingreview")).toBe(true);
    expect(awaitingReview("underreview")).toBe(true);
  });

  it("excludes decided outcomes the host has to act on", () => {
    // D-266 M4: `changesrequested`/`rejected` are decisions, not a queue. Counting them as "pending"
    // would tell an organiser to wait for something that already came back to them.
    expect(awaitingReview("changesrequested")).toBe(false);
    expect(awaitingReview("rejected")).toBe(false);
    expect(awaitingReview("draft")).toBe(false);
  });
});

describe("the open-host set itself", () => {
  it("contains exactly the five approved-onward states", () => {
    // Pinned as a set, not just probed: silently ADDING a state here is how a workspace opens early.
    expect([...OPEN_HOST_STATES].sort()).toEqual(
      ["closed", "completed", "live", "published", "scheduled"]
    );
  });
});
