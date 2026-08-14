import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import {
  Field,
  FormActions,
  FormBusy,
  FormErrorSummary,
  FormGroup,
  FormSteps,
  Input,
  Select,
  Textarea
} from "@kurx/ui";

/**
 * Guards on the form contract (`docs/ui-ux/accessibility-foundation.md` §3.2).
 *
 * Audit S1-1 measured `aria-describedby` and `aria-invalid` at ZERO occurrences
 * across all 115 web and admin screens: every validation failure in Kurx was
 * communicated by colour alone. These assert the wiring that closes it, at the
 * level it actually has to work — through `Field`, without the call site
 * changing.
 */

describe("Field — the S1-1 contract", () => {
  it("associates its label with the control it wraps", () => {
    render(
      <Field label="Phone number">
        <Input />
      </Field>
    );
    expect(screen.getByLabelText("Phone number")).toBeInTheDocument();
  });

  it("announces the error and links it to the control", () => {
    render(
      <Field label="Phone number" error="Enter a valid Indian mobile number">
        <Input />
      </Field>
    );
    const input = screen.getByLabelText("Phone number");
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Enter a valid Indian mobile number");
    // role=alert so it is announced when it appears, not only on focus.
    expect(screen.getByRole("alert")).toHaveTextContent("Enter a valid Indian mobile number");
  });

  it("links helper text when there is no error", () => {
    render(
      <Field label="Username" helper="Letters, numbers and underscores">
        <Input />
      </Field>
    );
    const input = screen.getByLabelText("Username");
    expect(input).toHaveAccessibleDescription("Letters, numbers and underscores");
    expect(input).not.toHaveAttribute("aria-invalid");
  });

  it("marks required in text as well as programmatically", () => {
    // A bare "*" has no meaning to a screen reader and these forms carry no legend.
    render(
      <Field label="Event title" required>
        <Input />
      </Field>
    );
    const input = screen.getByLabelText(/Event title/);
    expect(input).toHaveAttribute("aria-required", "true");
    expect(screen.getByText(/\(required\)/)).toBeInTheDocument();
  });

  it("wires Textarea and Select the same way", () => {
    render(
      <>
        <Field label="Description" error="Too short">
          <Textarea />
        </Field>
        <Field label="Category" error="Pick one">
          <Select>
            <option>A</option>
          </Select>
        </Field>
      </>
    );
    expect(screen.getByLabelText("Description")).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByLabelText("Category")).toHaveAttribute("aria-invalid", "true");
  });

  it("never overrides an id the caller set explicitly", () => {
    render(
      <Field label="Email">
        <Input id="my-own-id" />
      </Field>
    );
    expect(screen.getByLabelText("Email")).toHaveAttribute("id", "my-own-id");
  });

  it("gives each field a distinct id so two fields cannot collide", () => {
    render(
      <>
        <Field label="First name">
          <Input />
        </Field>
        <Field label="Last name">
          <Input />
        </Field>
      </>
    );
    const a = screen.getByLabelText("First name").getAttribute("id");
    const b = screen.getByLabelText("Last name").getAttribute("id");
    expect(a).toBeTruthy();
    expect(a).not.toBe(b);
  });
});

describe("FormGroup", () => {
  it("announces the group as context for the fields inside it", () => {
    render(
      <FormGroup legend="Venue" description="Where the event happens">
        <Field label="Address line 1">
          <Input />
        </Field>
      </FormGroup>
    );
    expect(screen.getByRole("group", { name: /Venue/ })).toBeInTheDocument();
  });
});

describe("FormErrorSummary", () => {
  it("lists failures as links to their fields and takes focus", () => {
    render(<FormErrorSummary errors={[{ id: "title", message: "Title is required" }]} />);
    const summary = screen.getByRole("alert");
    expect(summary).toHaveAttribute("tabindex", "-1");
    expect(screen.getByRole("link", { name: "Title is required" })).toHaveAttribute("href", "#title");
  });

  it("renders nothing when there is nothing to report", () => {
    const { container } = render(<FormErrorSummary errors={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe("FormBusy", () => {
  it("announces submission and disables the controls", () => {
    // A spinner inside a button is silent; without this a screen-reader user gets
    // no feedback between pressing submit and the response landing.
    render(
      <FormBusy busy label="Creating event">
        <Input aria-label="Title" />
      </FormBusy>
    );
    expect(screen.getByRole("status")).toHaveTextContent("Creating event");
    expect(screen.getByLabelText("Title")).toBeDisabled();
  });

  it("is inert when not busy", () => {
    render(
      <FormBusy busy={false}>
        <Input aria-label="Title" />
      </FormBusy>
    );
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Title")).not.toBeDisabled();
  });
});

describe("FormSteps", () => {
  it("exposes position structurally, not by colour", () => {
    render(<FormSteps steps={["Basics", "Tickets", "Publish"]} current={1} />);
    expect(screen.getByRole("navigation", { name: "Progress" })).toBeInTheDocument();
    expect(screen.getAllByRole("listitem")).toHaveLength(3);
    expect(screen.getByText("Tickets")).toHaveAttribute("aria-current", "step");
    expect(screen.getByText(/completed/)).toBeInTheDocument();
  });
});

describe("FormActions", () => {
  it("keeps the primary action last in the DOM and first on screen", () => {
    // Tab order and visual order legitimately differ: reverse-column puts the
    // primary under a thumb, while it stays the final tab stop.
    const { container } = render(
      <FormActions>
        <button type="button">Cancel</button>
        <button type="submit">Publish</button>
      </FormActions>
    );
    expect(container.firstElementChild?.className).toContain("flex-col-reverse");
    const buttons = screen.getAllByRole("button");
    expect(buttons[buttons.length - 1]).toHaveTextContent("Publish");
  });
});
