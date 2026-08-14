"use client";

import { useState } from "react";
import { CheckCircle2, CircleDashed, Globe, Lock } from "lucide-react";
import { Alert, Button } from "@kurx/ui";
import { CreateEventWizard } from "@/components/host/create-event-wizard";
import type { Category, FieldPreset, Representation } from "@/lib/api";

/**
 * The Create-Event gate (D-305).
 *
 * Creation used to open the eleven-step form the instant the button was pressed, and every eligibility
 * question was asked at **publish** — after the organiser had filled in eleven steps. The gate moves the
 * questions to the front so nobody starts work they cannot finish.
 *
 * Two screens, then the form:
 *
 *   ① Eligibility — reads the caller's EXISTING verification state and never re-asks a passed check.
 *   ② Public or Private — a capability decision, not a form field.
 *   ③ (deliberately absent) — resolved in D-305: Public and Private carry the SAME verification, so
 *      there is no second pass. The step is documented as skipped rather than silently omitted.
 *
 * **The gate never blocks a free event.** `TrustService` sets `canOrganizeFree = true` for any account
 * — "free/private events, no KYC" — so ① is informational for free hosting and only *paid* hosting has
 * a bar. Blocking creation on identity would invent a rule the platform does not have.
 */

type Product = "Public" | "Private";

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
  hostName
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
  /// D-307 — the gate's authority for Public. Never re-derived here: the same predicate gates the
  /// money path server-side, so a local guess that disagreed would promise what the server refuses.
  canCreatePublicEvent: boolean;
  canCreatePrivateEvent: boolean;
  hostName: string;
}) {
  const [stage, setStage] = useState<"eligibility" | "product" | "form">("eligibility");
  const [product, setProduct] = useState<Product | null>(null);

  if (stage === "form" && product) {
    return <CreateEventWizard
      representations={representations}
      canHostPaid={canHostPaid}
      categories={categories}
      subcategories={subcategories}
      presets={presets}
      product={product}
      hostName={hostName}
    />;
  }

  if (stage === "product") {
    return (
      <div className="space-y-6">
        <div>
          <h2 className="text-h2 text-text">What kind of event is this?</h2>
          <p className="mt-1 text-body text-muted">
            This decides which event types you can choose from. It isn&apos;t about who can see the
            event — you set that separately.
          </p>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <ProductCard
            selected={product === "Public"}
            onSelect={() => setProduct("Public")}
            icon={<Globe size={20} aria-hidden="true" />}
            title="Public"
            description="Open to people who don't already know you — meetups, fests, workshops, conferences, fundraisers."
            locked={!canCreatePublicEvent}
          />
          <ProductCard
            selected={product === "Private"}
            onSelect={() => setProduct("Private")}
            icon={<Lock size={20} aria-hidden="true" />}
            title="Private"
            description="For a group you already have — weddings, parties, reunions, internal company events."
            locked={!canCreatePrivateEvent}
          />
        </div>

        {/*
          D-307. A free public event still carries the platform's name and reaches every user through
          discovery, so Public asks for the full set whether or not money moves. Only the MISSING items
          are listed — a passed check is never asked for again.
        */}
        {product === "Public" && !canCreatePublicEvent ? (
          <div className="rounded-lg border border-border bg-surface p-4">
            <p className="text-label text-text">Verify your identity to host public events</p>
            <p className="mt-1 text-caption text-muted">
              Public events are open to everyone on Kurx, so we verify who is behind them — whether the
              event is free or paid.
            </p>
            <ul className="mt-3 space-y-1">
              <Sub done={identityVerified} label="Government ID or PAN approved" />
              <Sub done={panVerified} label="PAN approved" />
              <Sub done={bankVerified} label="Bank account approved" />
              {/* Penny drop and name match are the BANK OWNERSHIP links, named so the person knows what
                  is outstanding — they are inside bankVerified, not extra predicates. */}
              <Sub done={pennyDropPassed} label="Bank ownership confirmed (penny drop)" />
              <Sub done={bankNameMatched} label="Account holder name matches your PAN" />
            </ul>
            <a href="/settings/identity" className="mt-3 inline-block text-body text-accent-text hover:underline">
              Complete verification
            </a>
          </div>
        ) : null}

        {/* Private never asks for the financial chain: it cannot be listed, cannot take payment, and
            reaches no discovery surface, so there is nothing to bound and no money to settle. */}
        {product === "Private" ? (
          <Alert tone="info" title="Private events need no financial verification">
            A private event is invitation-only, never appears in search or on Home, and cannot sell
            tickets — so we don&apos;t ask for bank details.
          </Alert>
        ) : null}

        <div className="flex items-center justify-between border-t border-border pt-4">
          <Button variant="ghost" onClick={() => setStage("eligibility")}>Back</Button>
          <Button
            disabled={!product || (product === "Public" ? !canCreatePublicEvent : !canCreatePrivateEvent)}
            onClick={() => setStage("form")}
          >
            Continue
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-h2 text-text">Before you start</h2>
        <p className="mt-1 text-body text-muted">
          Here&apos;s what your account can do today. Anything already verified is done — you won&apos;t
          be asked again.
        </p>
      </div>

      <ul className="space-y-2">
        <Check done label="Create a free event" detail="Any signed-in account. Nothing to verify." />
        <Check
          done={canHostPaid}
          label="Sell tickets"
          detail={
            !canHostPaid
              ? "Needs identity, PAN and a verified bank account. You can create the event now and finish this before you publish."
              : identityVerified && panVerified && bankVerified
                ? "Identity, PAN and bank account are verified."
                : // D-323 — the capability is open while the proofs are not on file, which happens on
                  // exactly one condition: IDENTITY_VERIFICATION_BYPASS is set, and it cannot be set in
                  // Production. Saying "verified" here would be the one thing that decision forbids —
                  // the bypass relaxes what you may DO and must never restate it as something proved.
                  "Enabled without verification — this environment has the identity checks switched off. Nothing about your identity has been confirmed."
          }
        />
      </ul>

      {!canHostPaid ? (
        <div className="rounded-lg border border-border bg-surface p-4">
          <p className="text-label text-text">What&apos;s still needed to sell tickets</p>
          <ul className="mt-2 space-y-1">
            <Sub done={identityVerified} label="Government ID or PAN approved" />
            <Sub done={panVerified} label="PAN approved" />
            <Sub done={bankVerified} label="Bank account verified" />
          </ul>
          <a href="/settings/identity" className="mt-3 inline-block text-body text-accent-text hover:underline">
            Complete verification
          </a>
          {/* Deliberately not a blocker: a free event needs none of this, and stopping here would
              invent a requirement the platform does not have. */}
          <p className="mt-2 text-caption text-muted">
            You don&apos;t need any of this to create a free event.
          </p>
        </div>
      ) : null}

      <div className="flex justify-end border-t border-border pt-4">
        <Button onClick={() => setStage("product")}>Continue</Button>
      </div>
    </div>
  );
}

function ProductCard({
  selected, onSelect, icon, title, description, locked = false
}: {
  selected: boolean; onSelect: () => void; icon: React.ReactNode;
  title: string; description: string; locked?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={selected}
      className={`rounded-lg border p-4 text-left transition duration-fast ${
        selected ? "border-accent ring-1 ring-accent" : "border-border hover:border-accent/60"
      }`}
    >
      <span className="flex items-center gap-2 text-text">
        {icon}<strong>{title}</strong>
        {/* Selectable while locked, deliberately: choosing it is how the person SEES what is missing.
            Continue is what refuses, not the card. */}
        {locked ? <span className="text-caption text-muted">· needs verification</span> : null}
      </span>
      <span className="mt-2 block text-caption text-muted">{description}</span>
    </button>
  );
}

function Check({ done, label, detail }: { done: boolean; label: string; detail: string }) {
  return (
    <li className="flex items-start gap-3 rounded-lg border border-border bg-surface p-4">
      {done
        ? <CheckCircle2 size={18} className="mt-0.5 shrink-0 text-accent-text" aria-hidden="true" />
        : <CircleDashed size={18} className="mt-0.5 shrink-0 text-muted" aria-hidden="true" />}
      <span className="min-w-0">
        <span className="block text-body text-text">{label}</span>
        <span className="mt-0.5 block text-caption text-muted">{detail}</span>
      </span>
      <span className="sr-only">{done ? "available" : "not yet available"}</span>
    </li>
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
