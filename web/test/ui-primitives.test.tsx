import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import {
  Badge, Button, Checkbox, DateTimeField, Divider, HostTierBadge, IconButton, Input, Link, LinkButton,
  ListSkeleton, Progress, Radio, RadioGroup, Slider, Spinner, Switch
} from "@kurx/ui";

/**
 * Guards on the primitives' accessibility contract
 * (`docs/ui-ux/accessibility-foundation.md`).
 *
 * These assert the specific defects the Phase 1 audit measured, so they cannot
 * silently return — a class list is easy to "tidy" back into a failure.
 */

describe("Button", () => {
  it("labels the accent fill with the on-accent token, never a literal colour", () => {
    // The label has to follow whatever the fill measures to: ink under D-286's
    // ember (white on #F0762B was 2.86:1), white under D-288's #2563EB (5.17:1).
    // A literal `text-white` was right by accident once and wrong for years.
    render(<Button>Register</Button>);
    const cls = screen.getByRole("button", { name: "Register" }).className;
    expect(cls).toContain("text-on-accent");
    expect(cls).not.toContain("text-white");
  });

  it("gives secondary a boundary that can identify it as a control", () => {
    // WCAG 1.4.11: >= 3:1. `border` is decorative at 1.30:1 and cannot carry this.
    render(<Button variant="secondary">Cancel</Button>);
    const cls = screen.getByRole("button", { name: "Cancel" }).className;
    expect(cls).toContain("border-border-strong");
  });

  it("meets the 44px touch floor at its default size", () => {
    render(<Button>Book</Button>);
    expect(screen.getByRole("button", { name: "Book" }).className).toContain("h-11");
  });

  it("offers a shared destructive treatment", () => {
    render(<Button variant="danger">Delete event</Button>);
    expect(screen.getByRole("button", { name: "Delete event" }).className).toContain("bg-danger");
  });

  it("conveys disabled state to assistive tech, not just visually", () => {
    render(<Button disabled>Publish</Button>);
    expect(screen.getByRole("button", { name: "Publish" })).toBeDisabled();
  });
});

describe("IconButton", () => {
  it("requires a label and exposes it as the accessible name", () => {
    render(
      <IconButton label="Close">
        <svg />
      </IconButton>
    );
    // An icon-only control with no name is invisible to screen readers; the audit
    // found this in the web header, both overlay close buttons and the admin topbar.
    expect(screen.getByRole("button", { name: "Close" })).toBeInTheDocument();
  });

  it("renders a 44px box regardless of the icon inside it", () => {
    // The audited call sites were `p-2` around an 18px icon — 32-34px. Enforcing
    // the floor in the primitive means it cannot be got wrong per call site.
    render(
      <IconButton label="Notifications">
        <svg width={18} height={18} />
      </IconButton>
    );
    const cls = screen.getByRole("button", { name: "Notifications" }).className;
    expect(cls).toContain("h-11");
    expect(cls).toContain("w-11");
  });
});

describe("Link", () => {
  it("uses the text-safe ember, not the fill", () => {
    // `accent` is 2.80:1 on the light background — a fill, not a text colour.
    render(<Link href="/discover">Browse events</Link>);
    const cls = screen.getByRole("link", { name: "Browse events" }).className;
    expect(cls).toContain("text-accent-text");
    // Negative lookahead, not \b — a hyphen is a word boundary, so /\btext-accent\b/
    // matches inside "text-accent-text" and the guard would never fire.
    expect(cls).not.toMatch(/\btext-accent(?!-)/);
  });
});

describe("LinkButton", () => {
  it("is a real link, and shares the Button treatment", () => {
    render(<LinkButton href="/host/events/new">Create event</LinkButton>);
    const el = screen.getByRole("link", { name: "Create event" });
    expect(el).toHaveAttribute("href", "/host/events/new");
    expect(el.className).toContain("text-on-accent");
  });
});

describe("Checkbox / Radio", () => {
  it("wraps a native input so keyboard and form participation are free", () => {
    render(<Checkbox label="Email me about this event" />);
    expect(screen.getByRole("checkbox", { name: "Email me about this event" }).tagName).toBe("INPUT");
  });

  it("associates its label with the control", () => {
    // getByRole with an accessible name only resolves if htmlFor/id are wired.
    render(<Checkbox label="Send reminders" description="One day before" />);
    expect(screen.getByRole("checkbox", { name: /Send reminders/ })).toBeInTheDocument();
  });

  it("gives the control a boundary that identifies it", () => {
    render(<Checkbox label="Agree" />);
    expect(screen.getByRole("checkbox", { name: "Agree" }).className).toContain("border-border-strong");
  });

  it("groups radios under a legend so the choice is announced", () => {
    render(
      <RadioGroup legend="Ticket type">
        <Radio name="t" label="General" />
        <Radio name="t" label="VIP" />
      </RadioGroup>
    );
    expect(screen.getByRole("group", { name: "Ticket type" })).toBeInTheDocument();
    expect(screen.getAllByRole("radio")).toHaveLength(2);
  });
});

describe("Switch", () => {
  it("exposes switch semantics and state", () => {
    render(<Switch checked onChange={() => {}} label="Notifications" />);
    expect(screen.getByRole("switch", { name: "Notifications" })).toHaveAttribute("aria-checked", "true");
  });

  it("meets the touch floor", () => {
    render(<Switch checked={false} onChange={() => {}} label="Public profile" />);
    const cls = screen.getByRole("switch", { name: "Public profile" }).className;
    expect(cls).toContain("h-11");
    expect(cls).toContain("w-11");
  });
});

describe("Slider", () => {
  it("announces a formatted value rather than a bare number", () => {
    render(<Slider label="Max price" valueLabel="₹1,200" min={0} max={5000} defaultValue={1200} />);
    expect(screen.getByRole("slider", { name: "Max price" })).toHaveAttribute("aria-valuetext", "₹1,200");
  });
});

describe("Input", () => {
  it("marks an errored control invalid, not merely red", () => {
    // audit S1-1: `error` used to recolour the border and nothing else, so every
    // validation failure in Kurx was communicated by colour alone.
    render(<Input error aria-label="Phone" defaultValue="12" />);
    expect(screen.getByLabelText("Phone")).toHaveAttribute("aria-invalid", "true");
  });

  it("does not set aria-invalid when valid", () => {
    render(<Input aria-label="Email" />);
    expect(screen.getByLabelText("Email")).not.toHaveAttribute("aria-invalid");
  });

  it("uses a control boundary and body-size text", () => {
    render(<Input aria-label="Name" />);
    const cls = screen.getByLabelText("Name").className;
    expect(cls).toContain("border-border-strong");
    expect(cls).toContain("text-body");
  });
});

describe("Badge", () => {
  it("does not tint its background, because the tint failed contrast", () => {
    // Measured: every tone rendered its own token on a 15% tint of itself and
    // came out 3.61-4.62:1 across both themes — all below AA, and still below at
    // 10%. Tones now sit on `elevated`, which is exactly what Phase 4 solved for.
    render(<Badge tone="success">Published</Badge>);
    const cls = screen.getByText("Published").className;
    expect(cls).toContain("bg-elevated");
    expect(cls).not.toMatch(/bg-(success|danger|accent|warning|teal)\//);
  });

  it("uses the text-safe ember for the accent tone", () => {
    render(<Badge tone="accent">Live</Badge>);
    expect(screen.getByText("Live").className).toContain("text-accent-text");
  });

  it("treats Verified as an attestation, so teal rather than ember (D-286)", () => {
    render(<HostTierBadge tier={2} />);
    expect(screen.getByText("Verified").className).toContain("text-teal");
  });
});

describe("Progress", () => {
  it("announces its position rather than only drawing it", () => {
    render(<Progress label="Event setup" value={3} max={5} valueLabel="3 of 5 steps" />);
    const bar = screen.getByRole("progressbar", { name: "Event setup" });
    expect(bar).toHaveAttribute("aria-valuenow", "3");
    expect(bar).toHaveAttribute("aria-valuemax", "5");
    expect(bar).toHaveAttribute("aria-valuetext", "3 of 5 steps");
  });
});

describe("Spinner", () => {
  it("announces from a status role, not an aria-label on the svg", () => {
    // aria-label on an <svg> with no role is unreliably exposed, so the previous
    // version was silent on several screen readers.
    render(<Spinner label="Loading tickets" />);
    expect(screen.getByRole("status")).toHaveTextContent("Loading tickets");
  });
});

describe("Skeleton", () => {
  it("hides placeholders and marks the region busy", () => {
    render(<ListSkeleton rows={2} label="Loading events" />);
    const region = screen.getByRole("status");
    expect(region).toHaveAttribute("aria-busy", "true");
    expect(region).toHaveTextContent("Loading events");
  });
});

describe("Divider", () => {
  it("is decorative by default so it is not announced between every card", () => {
    const { container } = render(<Divider />);
    expect(container.firstElementChild).toHaveAttribute("role", "presentation");
  });

  it("becomes a real separator when it carries a label", () => {
    render(<Divider label="or" />);
    expect(screen.getByRole("separator", { name: "or" })).toBeInTheDocument();
  });
});

describe("DateTimeField", () => {
  it("gives the native picker an accessible name and describes its format", () => {
    render(<DateTimeField label="Starts at" helper="Local time" />);
    const input = screen.getByLabelText(/Starts at/);
    expect(input).toHaveAttribute("type", "datetime-local");
    expect(input).toHaveAccessibleDescription("Local time");
  });

  it("wires an error to the control and announces it", () => {
    render(<DateTimeField label="Ends at" error="Must be after the start time" />);
    const input = screen.getByLabelText(/Ends at/);
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Must be after the start time");
    expect(screen.getByRole("alert")).toBeInTheDocument();
  });
});
