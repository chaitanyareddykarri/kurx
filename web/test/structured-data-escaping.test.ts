import { describe, expect, it } from "vitest";

import { jsonLd } from "@/lib/site";

/**
 * `jsonLd`'s output goes straight into `dangerouslySetInnerHTML` on a
 * `<script type="application/ld+json">`, on six public pages — three of which pass attacker-supplied
 * strings: `/u/{username}` (the display name typed at onboarding), `/e/{slug}` (event title, venue)
 * and `/o/{slug}` (organization name, bio).
 *
 * `JSON.stringify` does not escape `<`. An HTML parser ends a `<script>` at the first literal
 * `</script>` it sees — being inside a JSON string means nothing to it — so a display name of
 * `</script><img src=x onerror=…>` closed the tag early and everything after it was parsed as
 * markup. Stored XSS on a public page, reachable by any registered user with no privileges.
 *
 * These assert the property rather than the implementation: what must hold is that **no literal `<`
 * survives into the HTML**, whatever escaping achieves it.
 */
describe("jsonLd cannot escape its script element", () => {
  it("emits no literal '<' for a payload that closes the tag", () => {
    const { __html } = jsonLd("Person", { name: '</script><img src=x onerror=alert(1)>' });
    expect(__html).not.toContain("<");
    expect(__html.toLowerCase()).not.toContain("</script");
  });

  it("still parses back to the original value", () => {
    // Escaping must not corrupt the data — `<` is a JSON escape, so a parser round-trips it.
    const evil = '</script><img src=x>';
    const parsed = JSON.parse(jsonLd("Person", { name: evil }).__html) as { name: string };
    expect(parsed.name).toBe(evil);
  });

  it("escapes '<' wherever it appears, including the type and nested values", () => {
    const { __html } = jsonLd("Event", { location: { name: "<b>Hall</b>" }, list: ["<script>"] });
    expect(__html).not.toContain("<");
  });

  it("leaves ordinary structured data intact", () => {
    const { __html } = jsonLd("SoftwareApplication", { name: "Kurx" });
    expect(JSON.parse(__html)).toEqual({
      "@context": "https://schema.org",
      "@type": "SoftwareApplication",
      name: "Kurx"
    });
  });
});
