"use client";

import { useState } from "react";
import { CheckCircle2, CircleDashed, Gift, Globe, Lock, Ticket } from "lucide-react";
import { Alert, Button } from "@kurx/ui";
import { CreateEventWizard } from "@/components/host/create-event-wizard";
import type { Category, FieldPreset, Representation } from "@/lib/api";

/**
 * The Create-Event gate (D-305, tiered by D-343).
 *
 * Creation used to open the eleven-step form the instant the button was pressed, and every eligibility
 * question was asked at **publish** — after the organiser had filled in eleven steps. The gate moves the
 * questions to the front so nobody starts work they cannot finish.
 *
 * Two screens, then the form:
 *
 *   ① Who is it for?   Public or Private — a capability decision, not a form field.
 *   ② Are you charging? Free or Paid, with the requirements THAT pair actually needs.
 *
 * **The two questions select a verification tier between them (D-343):**
 *
 * | Product | Money | Requires                                                    |
 * |---------|-------|-------------------------------------------------------------|
 * | Private | Free  | nothing                                                     |
 * | Public  | Free  | identity — government ID *or* PAN                           |
 * | Public  | Paid  | identity + PAN + bank + penny drop + holder-name match       |
 * | Private | Paid  | impossible — a Private event can never sell                 |
 *
 * The screen this replaced ("Before you start") listed those same two capabilities as bordered cards
 * with circled check icons — the exact chrome the real selectors use one screen later — so people
 * clicked them expecting to choose free or paid, and nothing happened. The information was right and
 * the affordance was a lie. Now the choice is real and the requirement is stated next to it.
 */

type Product = "Public" | "Private";
type Pricing = "free" | "paid";

export function CreateEventGate({
  representations,
  canHostPaid,
  categories,
  subcategories,
  presets,
  identityVerified,
  panVerified,
  bankVerified,
  pennyDropPassed,
  bankNameMatched,
  canCreatePublicEvent,
  canCreatePrivateEvent,
  requiresRepresentation,
  representativeRoles
}: {
  representations: Representation[];
  canHostPaid: boolean;
  categories: Category[];
  subcategories: Category[];
  presets: FieldPreset[];
  identityVerified: boolean;
  panVerified: boolean;
  bankVerified: boolean;
  pennyDropPassed: boolean;
  bankNameMatched: boolean;
  /// D-307/D-343 — the server's authority for Public, now the IDENTITY tier alone. Never re-derived
  /// here: the same predicate gates the path server-side, so a local guess that disagreed would
  /// promise what the server refuses.
  canCreatePublicEvent: boolean;
  canCreatePrivateEvent: boolean;
  /// The closed representative-role vocabulary, forwarded to the wizard's Authorization step (D-351).
  /// Fetched server-side and never copied here — a client list that drifts offers a role the API refuses.
  /// D-353/D-352 — whether a Public event must name a verified organization. False only under the
  /// dev bypass; the gate then stops demanding one, matching what the server will actually accept.
  requiresRepresentation: boolean;
  representativeRoles: string[];
}) {
  const [stage, setStage] = useState<"product" | "pricing" | "form">("product");
  const [product, setProduct] = useState<Product | null>(null);
  const [pricing, setPricing] = useState<Pricing>("free");

  /// A Private event can never sell, so choosing it resets the money answer rather than carrying a
  /// stale "paid" into a product that cannot honour it.
  function chooseProduct(next: Product) {
    setProduct(next);
    if (next === "Private") setPricing("free");
  }

  if (stage === "form" && product) {
    return <CreateEventWizard
      representations={representations}
      canHostPaid={canHostPaid}
      categories={categories}
      subcategories={subcategories}
      presets={presets}
      product={product}
      initialPricing={pricing}
      requiresRepresentation={requiresRepresentation}
      representativeRoles={representativeRoles}
    />;
  }

  if (stage === "pricing" && product) {
    const isPublic = product === "Public";
    // D-350 — selling also needs a VERIFIED organization to represent, because settlement is keyed on
    // one. The server enforces this at submit-for-review and again at capture; asking here is what
    // stops someone filling in eleven steps for an event that can never sell.
    // D-352 — the capability, not the fact: under the dev bypass the server accepts an unverified
    // org, and gating on `is_verified` here would refuse what the server would have taken.
    const verifiedReps = representations.filter((r) => r.can_back_paid_event ?? r.is_verified);
    // Under the bypass the server accepts a paid event with no organization at all, so requiring one
    // here would refuse what the server would take — the exact class of bug D-352 fixed for `is_verified`.
    const canRepresentVerified = !requiresRepresentation || verifiedReps.length > 0;
    // Public is refused by the identity tier; Paid by the financial one, and now also by having no
    // verified organization. Kept as separate reasons because they are answered in three different
    // places and a merged message would send someone to the wrong one.
    const publicBlocked = isPublic && !canCreatePublicEvent;
    const paidBlocked = pricing === "paid" && (!canHostPaid || !canRepresentVerified);

    return (
      <div className="space-y-6">
        <div>
          <h2 className="text-h2 text-text">Are you charging for tickets?</h2>
          <p className="mt-1 text-body text-muted">
            {isPublic
              ? "A free public event only needs to prove who you are. Selling tickets also needs the bank account the money settles into."
              : "Private events are always free — they can’t sell tickets."}
          </p>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <ChoiceCard
            selected={pricing === "free"}
            onSelect={() => setPricing("free")}
            icon={<Gift size={20} aria-hidden="true" />}
            title="Free"
            description="No ticket charges. Anyone can register."
          />
          <ChoiceCard
            selected={pricing === "paid"}
            onSelect={() => isPublic && setPricing("paid")}
            disabled={!isPublic}
            icon={<Ticket size={20} aria-hidden="true" />}
            title="Paid"
            description={isPublic
              ? "Sell tickets. You set the price in the form."
              : "Not available for a private event."}
            note={!isPublic ? undefined
              : !canHostPaid ? "needs bank verification"
                : !canRepresentVerified ? "needs a verified organization"
                  : undefined}
          />
        </div>

        {/* Private asks for nothing at all — it cannot be listed, cannot take payment, and reaches no
            discovery surface, so there is no exposure to bound and no money to settle. */}
        {!isPublic ? (
          <Alert tone="info" title="Nothing to verify">
            A private event is invitation-only, never appears in search or on Home, and cannot sell
            tickets — so we don’t ask for identity or bank details.
          </Alert>
        ) : (
          <div className="rounded-lg border border-border bg-surface p-4">
            <p className="text-label text-text">
              {pricing === "paid" ? "What selling tickets needs" : "What a free public event needs"}
            </p>
            <p className="mt-1 text-caption text-muted">
              {pricing === "paid"
                ? "Money settles into a bank account, so we confirm the account is real and yours."
                : "Public events reach everyone on Kurx through search and Home, so we verify who is behind them. We don’t ask for bank details — nothing is being paid."}
            </p>
            <ul className="mt-3 space-y-1">
              {/* The identity tier — the whole bar for a free public event. PAN is deliberately not
                  named here: it is a tax identity and a free event reports no income (D-343). */}
              <Sub done={identityVerified} label="Government ID or PAN approved" />
              {/* The financial tier, shown only when money is actually involved. Penny drop and name
                  match are the bank-OWNERSHIP links inside bankVerified, named so the person knows
                  what is outstanding — they are not extra predicates. */}
              {pricing === "paid" ? (
                <>
                  <Sub done={panVerified} label="PAN approved" />
                  <Sub done={bankVerified} label="Bank account approved" />
                  <Sub done={pennyDropPassed} label="Bank ownership confirmed (penny drop)" />
                  <Sub done={bankNameMatched} label="Account holder name matches your PAN" />
                  {/* D-350 — not a proof about the person: ticket money settles to an organization, so
                      a paid event must represent a verified one. Listed alongside the personal proofs
                      because to the organiser it is one checklist, not two systems. */}
                  <Sub done={canRepresentVerified} label="A verified organization to represent" />
                </>
              ) : null}
            </ul>

            {/* Spelled out rather than left as a ticked-off row: this is the one requirement that is not
                about the person, and "you cannot sell as yourself" is surprising enough to deserve the
                sentence. Shown only when it is the thing standing in the way. */}
            {pricing === "paid" && canHostPaid && !canRepresentVerified ? (
              <p className="mt-3 text-caption text-muted">
                Ticket money settles into an organization&apos;s account, so a paid event has to
                represent one — you can&apos;t sell as yourself. Free events have no such requirement.
              </p>
            ) : null}
            {publicBlocked || paidBlocked ? (
              <a href="/settings/identity" className="mt-3 inline-block text-body text-accent-text hover:underline">
                Complete verification
              </a>
            ) : null}
          </div>
        )}

        {/* D-323 — the capability is open while the proofs are not on file, which happens on exactly one
            condition: IDENTITY_VERIFICATION_BYPASS is set, and it cannot be set in Production. Saying
            "verified" would forge the fact the bypass is explicitly forbidden from forging. */}
        {isPublic && canCreatePublicEvent && !identityVerified ? (
          <Alert tone="warning" title="Enabled without verification">
            This environment has the identity checks switched off. Nothing about your identity has been
            confirmed.
          </Alert>
        ) : null}

        <div className="flex items-center justify-between border-t border-border pt-4">
          <Button variant="ghost" onClick={() => setStage("product")}>Back</Button>
          <Button disabled={publicBlocked || paidBlocked} onClick={() => setStage("form")}>
            Continue
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-h2 text-text">Who is this event for?</h2>
        <p className="mt-1 text-body text-muted">
          This decides which event types you can choose from. It isn’t about who can see the event —
          you set that separately.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <ChoiceCard
          selected={product === "Public"}
          onSelect={() => chooseProduct("Public")}
          icon={<Globe size={20} aria-hidden="true" />}
          title="Public"
          description="Open to people who don't already know you — meetups, fests, workshops, conferences, fundraisers."
          note={!canCreatePublicEvent ? "needs verification" : undefined}
        />
        <ChoiceCard
          selected={product === "Private"}
          onSelect={() => chooseProduct("Private")}
          icon={<Lock size={20} aria-hidden="true" />}
          title="Private"
          description="For a group you already have — weddings, parties, reunions, internal company events."
          note={!canCreatePrivateEvent ? "needs verification" : undefined}
        />
      </div>

      <div className="flex justify-end border-t border-border pt-4">
        <Button disabled={!product} onClick={() => setStage("pricing")}>Continue</Button>
      </div>
    </div>
  );
}

function ChoiceCard({
  selected, onSelect, icon, title, description, note, disabled = false
}: {
  selected: boolean; onSelect: () => void; icon: React.ReactNode;
  title: string; description: string; note?: string; disabled?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={selected}
      disabled={disabled}
      className={`rounded-lg border p-4 text-left transition duration-fast ${
        disabled
          ? "cursor-not-allowed border-border opacity-50"
          : selected
            ? "border-accent ring-1 ring-accent"
            : "border-border hover:border-accent/60"
      }`}
    >
      <span className="flex items-center gap-2 text-text">
        {icon}<strong>{title}</strong>
        {/* Selectable while unverified, deliberately: choosing it is how the person SEES what is
            missing. Continue is what refuses, not the card. */}
        {note ? <span className="text-caption text-muted">· {note}</span> : null}
      </span>
      <span className="mt-2 block text-caption text-muted">{description}</span>
    </button>
  );
}

function Sub({ done, label }: { done: boolean; label: string }) {
  return (
    <li className="flex items-center gap-2 text-caption">
      {done
        ? <CheckCircle2 size={14} className="text-accent-text" aria-hidden="true" />
        : <CircleDashed size={14} className="text-muted" aria-hidden="true" />}
      <span className={done ? "text-text" : "text-muted"}>{label}</span>
      <span className="sr-only">{done ? "done" : "outstanding"}</span>
    </li>
  );
}
