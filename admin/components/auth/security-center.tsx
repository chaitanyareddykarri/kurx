"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@kurx/ui";
import {
  revokeBrowserAction,
  revokeDeviceAction,
  revokeSessionAction,
  signOutEverywhereAction,
  generateRecoveryCodesAction
} from "@/lib/security-actions";
import { relativeTime, securityActivityLabel } from "@/lib/security-activity";
import { StepUpRequired } from "@/components/auth/step-up-required";
import type {
  DeviceView,
  SecurityActivityItem,
  SecurityOverview,
  SessionView,
  TrustedBrowserView
} from "@/lib/api";

/**
 * Staff Security Center (Phase 2E), parity with the web app. Data is fetched server-side and passed in;
 * every write goes through a server action, then router.refresh() re-runs the server component to show
 * the new state — so the session token never reaches the browser.
 */
export function StaffSecurityCenter({
  overview,
  sessions,
  devices,
  browsers,
  recoveryRemaining,
  activity
}: {
  overview: SecurityOverview | null;
  sessions: SessionView[];
  devices: DeviceView[];
  browsers: TrustedBrowserView[];
  recoveryRemaining: number | null;
  activity: SecurityActivityItem[];
}) {
  const router = useRouter();
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [freshCodes, setFreshCodes] = useState<string[] | null>(null);
  const [stepUp, setStepUp] = useState(false);

  async function act(id: string, action: () => Promise<{ ok: boolean; error?: string }>) {
    setError("");
    setBusy(id);
    const res = await action();
    setBusy("");
    if (res.ok) router.refresh();
    else setError(res.error ?? "Something went wrong.");
  }

  async function signOutAll() {
    if (!confirm("Sign out of every session and forget every trusted browser, including this one?")) return;
    setBusy("signout");
    const res = await signOutEverywhereAction();
    if (res.ok) router.replace("/login");
    else {
      setError(res.error);
      setBusy("");
    }
  }

  async function makeCodes() {
    setError("");
    setBusy("codes");
    const res = await generateRecoveryCodesAction();
    setBusy("");
    if (res.ok) {
      setFreshCodes(res.codes);
      setStepUp(false);
      router.refresh();
    } else if (res.error === "step_up_required") {
      // The action needs a fresh device confirmation (AM6). Show the step-up flow, which retries once satisfied.
      setStepUp(true);
    } else {
      setError(res.error);
    }
  }

  return (
    <div className="space-y-8">
      {error ? <p role="alert" className="rounded-md border border-danger/40 bg-danger/10 p-3 text-sm text-danger">{error}</p> : null}

      {/* Overview */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-text">Overview</h2>
        {overview ? (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Stat label="Password" value={overview.has_password ? "Set" : "Not set"} ok={overview.has_password} />
            <Stat label="Email" value={overview.email_verified ? "Verified" : overview.email ? "Unverified" : "Not added"} ok={overview.email_verified} />
            <Stat label="Phone" value={overview.phone_verified ? "Verified" : "Unverified"} ok={overview.phone_verified} />
            <Stat label="Passkeys" value={String(overview.passkeys)} ok={overview.passkeys > 0} />
            <Stat label="Trusted devices" value={String(overview.trusted_devices)} ok={overview.trusted_devices > 0} />
            <Stat label="Trusted browsers" value={String(overview.trusted_browsers)} ok />
            <Stat label="Active sessions" value={String(overview.active_sessions)} ok />
            <Stat label="Recovery codes" value={String(overview.recovery_codes_remaining)} ok={overview.recovery_codes_remaining > 0} />
          </div>
        ) : (
          <p className="text-sm text-muted">Couldn&apos;t load your overview.</p>
        )}
      </section>

      {/* Trusted devices */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-text">Trusted devices</h2>
        {devices.length === 0 ? <p className="text-sm text-muted">No trusted devices.</p> : null}
        <List>
          {devices.map((d) => (
            <Row
              key={d.id}
              title={d.name ?? "Unnamed"}
              subtitle={`${d.platform} · ${d.state}`}
              busy={busy === d.id}
              onRemove={() => {
                if (confirm("Remove this device? Any session it holds is signed out immediately.")) void act(d.id, () => revokeDeviceAction(d.id));
              }}
            />
          ))}
        </List>
      </section>

      {/* Trusted browsers */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-text">Trusted browsers</h2>
        {browsers.length === 0 ? <p className="text-sm text-muted">No trusted browsers.</p> : null}
        <List>
          {browsers.map((b) => (
            <Row
              key={b.id}
              title={[b.browser, b.operatingSystem].filter(Boolean).join(" · ") || b.label || "Browser"}
              subtitle={[b.isCurrent ? "this browser" : null, b.approxLocation ?? b.ip, b.lastUsedAt ? `last used ${relativeTime(b.lastUsedAt)}` : `added ${relativeTime(b.createdAt)}`].filter(Boolean).join(" · ")}
              busy={busy === b.id}
              onRemove={() => {
                if (confirm("Forget this browser? It will need a second factor again.")) void act(b.id, () => revokeBrowserAction(b.id));
              }}
            />
          ))}
        </List>
      </section>

      {/* Sessions */}
      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-text">Where you&apos;re signed in</h2>
          {sessions.length > 0 ? (
            <Button variant="secondary" disabled={busy === "signout"} onClick={signOutAll}>
              {busy === "signout" ? "Signing out…" : "Sign out everywhere"}
            </Button>
          ) : null}
        </div>
        {sessions.length === 0 ? <p className="text-sm text-muted">No other active sessions.</p> : null}
        <List>
          {sessions.map((s) => (
            <Row
              key={s.id}
              title={`${s.deviceName ?? "Unknown device"}${s.isCurrent ? " (this device)" : ""}`}
              subtitle={`${s.platform ?? "unknown"} · started ${relativeTime(s.createdAt)}`}
              busy={busy === s.id}
              removeLabel="Sign out"
              onRemove={() => void act(s.id, () => revokeSessionAction(s.id))}
            />
          ))}
        </List>
      </section>

      {/* Recovery codes */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-text">Recovery codes</h2>
        <p className="text-sm text-muted">One-time codes to get back in if you lose every device. {recoveryRemaining !== null ? `${recoveryRemaining} unused.` : ""}</p>
        {freshCodes ? (
          <div className="space-y-3 rounded-lg border border-border p-4">
            <p className="text-sm font-medium text-text">Save these now — they won&apos;t be shown again. Any previous set was revoked.</p>
            <ul className="grid grid-cols-2 gap-2 font-mono text-sm text-text">
              {freshCodes.map((c) => (
                <li key={c}>{c}</li>
              ))}
            </ul>
            <div className="flex gap-2">
              <Button variant="secondary" onClick={() => void navigator.clipboard.writeText(freshCodes.join("\n"))}>Copy</Button>
              <Button onClick={() => setFreshCodes(null)}>I&apos;ve saved them</Button>
            </div>
          </div>
        ) : stepUp ? (
          <StepUpRequired
            reason="Generating recovery codes is a high-security action, so we need a fresh confirmation from a trusted device."
            onCancel={() => setStepUp(false)}
            onSatisfied={async () => {
              await makeCodes();
            }}
          />
        ) : (
          <Button variant="secondary" disabled={busy === "codes"} onClick={makeCodes}>
            {busy === "codes" ? "Generating…" : recoveryRemaining ? "Replace recovery codes" : "Generate recovery codes"}
          </Button>
        )}
      </section>

      {/* Security activity */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-text">Recent security activity</h2>
        {activity.length === 0 ? (
          <p className="text-sm text-muted">Nothing recent.</p>
        ) : (
          <List>
            {activity.map((item, index) => (
              <li key={`${item.type}-${item.created_at}-${index}`} className="flex items-center gap-3 p-3">
                <span className={`h-2 w-2 shrink-0 rounded-full ${item.severity === "critical" ? "bg-danger" : item.severity === "warning" ? "bg-amber-500" : "bg-accent"}`} aria-hidden />
                <span className="flex-1 truncate text-sm text-text">{securityActivityLabel(item.type)}</span>
                <span className="shrink-0 text-xs text-muted">{relativeTime(item.created_at)}</span>
              </li>
            ))}
          </List>
        )}
      </section>
    </div>
  );
}

function Stat({ label, value, ok }: { label: string; value: string; ok: boolean }) {
  return (
    <div className="rounded-md border border-border bg-background p-3">
      <p className="text-xs text-muted">{label}</p>
      <p className={`mt-0.5 text-sm font-semibold ${ok ? "text-text" : "text-danger"}`}>{value}</p>
    </div>
  );
}

function List({ children }: { children: React.ReactNode }) {
  const items = Array.isArray(children) ? children : [children];
  if (items.filter(Boolean).length === 0) return null;
  return <ul className="divide-y divide-border rounded-lg border border-border">{children}</ul>;
}

function Row({
  title,
  subtitle,
  busy,
  onRemove,
  removeLabel = "Remove"
}: {
  title: string;
  subtitle: string;
  busy: boolean;
  onRemove: () => void;
  removeLabel?: string;
}) {
  return (
    <li className="flex items-center justify-between gap-4 p-3">
      <div className="min-w-0">
        <p className="truncate text-sm font-medium text-text">{title}</p>
        <p className="truncate text-xs text-muted">{subtitle}</p>
      </div>
      <Button variant="secondary" disabled={busy} onClick={onRemove}>
        {busy ? "…" : removeLabel}
      </Button>
    </li>
  );
}
