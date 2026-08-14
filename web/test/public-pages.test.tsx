import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { InfoPage } from "@/components/marketing/info-page";

/**
 * Guards on the public marketing frame (Phase 13A).
 *
 * The regression these exist to prevent is one this program itself introduced:
 * Phase 11 gave the `(public)` layout a `<main id="main-content">` for the skip
 * link, but all eight pages inside it already rendered their own `<main>`. Two
 * nested main landmarks is invalid, and it leaves the skip link with two
 * possible destinations.
 */

describe("InfoPage", () => {
  it("renders no main landmark of its own — the layout owns the only one", () => {
    const { container } = render(<InfoPage title="About Kurx" intro="Something about the product." />);
    expect(container.querySelector("main")).toBeNull();
  });

  it("exposes exactly one h1", () => {
    render(<InfoPage title="About Kurx" intro="Something." />);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("emits structured data only when a type is given", () => {
    const { container: withType } = render(
      <InfoPage title="Privacy" intro="How we handle data." schemaType="PrivacyPolicy" />
    );
    expect(withType.querySelector('script[type="application/ld+json"]')).not.toBeNull();

    const { container: without } = render(<InfoPage title="Pricing" intro="Three plans." />);
    expect(without.querySelector('script[type="application/ld+json"]')).toBeNull();
  });

  it("renders supplied content below the intro", () => {
    render(
      <InfoPage title="Features" intro="What Kurx does.">
        <p>Extra body</p>
      </InfoPage>
    );
    expect(screen.getByText("Extra body")).toBeInTheDocument();
  });
});
