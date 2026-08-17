"use client";

import { useCallback, useEffect, useState } from "react";
import {
  generateRecoveryCodes,
  listDevices,
  listPasskeys,
  listSessions,
  listTrustedBrowsers,
  passkeyRegister,
  passkeyRegisterOptions,
  recoveryCodesRemaining,
  revokeDevice,
  revokeSession,
  revokeTrustedBrowser,
  securityActivity,
  securityOverview,
  signOutEverywhere,
  type DeviceView,
  type SecurityActivityItem,
  type SecurityOverview,
  type SessionView,
  type TrustedBrowserView
} from "@/lib/auth-api";
import { createPasskey, hasPlatformAuthenticator, isPasskeySupported, passkeyErrorMessage } from "@/lib/webauthn";
import { apiErrorMessage, apiErrorStatus } from "@/lib/api";
import { logoutAction } from "@/lib/actions";
import { relativeTime, securityActivityLabel } from "@/lib/security-activity";
import { Button } from "@/components/ui/button";
import { PasswordManager } from "@/components/auth/password-manager";
import { StepUpRequired } from "@/components/auth/step-up-required";

/**
 * Security Center (Phase 2E) — the one place a user manages every authentication factor and channel:
 * an overview, password, passkeys, trusted devices, trusted browsers, active sessions, recovery codes,
 * their own recent security activity, and the way back in (account recovery). Each section is a view
 * over its own backend endpoint; the overview call summarizes the counts so the page loads coherently.
 */
export function SecurityCenter({ accessToken }: { accessToken: string }) {
  const [overview, setOverview] = useState<SecurityOverview | null>(null);
  const [passkeys, setPasskeys] = useState<DeviceView[]>([]);
  const [devices, setDevices] = useState<DeviceView[]>([]);
  const [sessions, setSessions] = useState<SessionView[]>([]);
  const [browsers, setBrowsers] = useState<TrustedBrowserView[]>([]);
  const [remaining, setRemaining] = useState<number | null>(null);
  const [activity, setActivity] = useState<SecurityActivityItem[]>([]);
  const [freshCodes, setFreshCodes] = useState<string[] | null>(null);
  const [stepUp, setStepUp] = useState(false);
  const [passkeySupported, setPasskeySupported] = useState(false);
  const [platformName, setPlatformName] = useState("this device");
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const refresh = useCallback(async () => {
    const [o, p, d, s, b, r, a] = await Promise.all([
      securityOverview(accessToken).catch(() => null),
      listPasskeys(accessToken).catch(() => []),
      listDevices(accessToken).catch(() => []),
      listSessions(accessToken).catch(() => []),
      listTrustedBrowsers(accessToken).catch(() => []),
      recoveryCodesRemaining(accessToken).catch(() => null),
      securityActivity(accessToken, 25).catch(() => [])
    ]);
    setOverview(o);
    setPasskeys(p);
    // A passkey is also a trusted device server-side; show it in one list only to avoid confusion.
    setDevices(d.filter((device) => !p.some((pk) => pk.id === device.id)));
    setSessions(s);
    setBrowsers(b);
    setRemaining(r);
    setActivity(a);
  }, [accessToken]);

  useEffect(() => {
    void refresh();
    setPasskeySupported(isPasskeySupported());
    void hasPlatformAuthenticator().then((has) => setPlatformName(has ? "this device" : "your security key"));
  }, [refresh]);

  async function run(id: string, action: () => Promise<void>, ok?: string) {
    setError("");
    setNotice("");
    setBusy(id);
    try {
      await action();
      if (ok) setNotice(ok);
      await refresh();
    } catch (err) {
      setError(apiErrorMessage(err));
    } finally {
      setBusy("");
    }
  }

  async function addPasskey() {
    setError("");
    setNotice("");
    setBusy("passkey");
    try {
      const { challenge_id, options } = await passkeyRegisterOptions(accessToken);
      const attestation = await createPasskey(options as never);
      await passkeyRegister(accessToken, challenge_id, attestation, defaultPasskeyName());
      setNotice("Passkey added.");
      await refresh();
    } catch (err) {
      setError(err instanceof DOMException ? passkeyErrorMessage(err) : apiErrorMessage(err));
    } finally {
      setBusy("");
    }
  }

  function removeDevice(id: string, label: string) {
    if (!confirm(`Remove ${label}? Any session it holds will be signed out immediately.`)) return;
    void run(id, () => revokeDevice(accessToken, id), `${label} removed.`);
  }

  function removeBrowser(id: string, label: string) {
    if (!confirm(`Forget ${label}? It will have to sign in with a second factor again.`)) return;
    void run(id, () => revokeTrustedBrowser(accessToken, id), `${label} forgotten.`);
  }

  async function makeRecoveryCodes() {
    setError("");
    setNotice("");
    setBusy("codes");
    try {
      setFreshCodes(await generateRecoveryCodes(accessToken));
      setStepUp(false);
      await refresh();
    } catch (err) {
      // 403 here is the step-up gate (AM6): the action needs a fresh device confirmation. Show the
      // step-up flow rather than a dead-end message; it retries this action once satisfied.
      if (apiErrorStatus(err) === 403) setStepUp(true);
      else setError(apiErrorMessage(err));
    } finally {
      setBusy("");
    }
  }

  async function signOutAll() {
    if (!confirm("Sign out of every session and forget every trusted browser, including this one?")) return;
    setBusy("signout");
    try {
      await signOutEverywhere(accessToken);
      await logoutAction(); // ends this session locally too and redirects to home
    } catch (err) {
      setError(apiErrorMessage(err));
      setBusy("");
    }
  }

  function downloadCodes(codes: string[]) {
    const blob = new Blob([`Kurx recovery codes\n\n${codes.join("\n")}\n`], { type: "text/plain" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "kurx-recovery-codes.txt";
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="space-y-10">
      {error ? <p className="rounded-md border border-danger/40 bg-danger/5 p-3 text-sm text-danger" role="alert">{error}</p> : null}
      {notice ? <p className="text-sm text-muted" role="status">{notice}</p> : null}

      {/* 1 — Overview */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Overview</h2>
        {overview ? (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Stat label="Password" value={overview.has_password ? "Set" : "Not set"} ok={overview.has_password} />
            <Stat
              label="Email"
              value={overview.email_verified ? "Verified" : overview.email ? "Unverified" : "Not added"}
              ok={overview.email_verified}
            />
            <Stat label="Phone" value={overview.phone_verified ? "Verified" : "Unverified"} ok={overview.phone_verified} />
            <Stat label="Passkeys" value={String(overview.passkeys)} ok={overview.passkeys > 0} />
            <Stat label="Trusted devices" value={String(overview.trusted_devices)} ok={overview.trusted_devices > 0} />
            <Stat label="Trusted browsers" value={String(overview.trusted_browsers)} ok />
            <Stat label="Active sessions" value={String(overview.active_sessions)} ok />
            <Stat label="Recovery codes" value={String(overview.recovery_codes_remaining)} ok={overview.recovery_codes_remaining > 0} />
          </div>
        ) : (
          <p className="text-sm text-muted">Loading…</p>
        )}
      </section>

      {/* 2 — Password */}
      <section>
        <PasswordManager accessToken={accessToken} />
      </section>

      {/* 3 — Passkeys */}
      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold">Passkeys</h2>
            <p className="text-sm text-muted">Sign in with {platformName} instead of a code. Passkeys can&apos;t be phished.</p>
          </div>
          {passkeySupported ? (
            <Button type="button" onClick={addPasskey} disabled={busy === "passkey"}>
              {busy === "passkey" ? "Waiting…" : "Add passkey"}
            </Button>
          ) : null}
        </div>
        <ItemList items={passkeys} empty="No passkeys yet." busyId={busy} onRemove={(d) => removeDevice(d.id, d.name ?? "this passkey")} />
      </section>

      {/* 4 — Trusted devices */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Trusted devices</h2>
        <p className="text-sm text-muted">Devices that can approve your sign-ins. Removing one signs it out immediately.</p>
        <ItemList items={devices} empty="No trusted devices yet." busyId={busy} onRemove={(d) => removeDevice(d.id, d.name ?? "this device")} />
      </section>

      {/* 5 — Trusted browsers */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Trusted browsers</h2>
        <p className="text-sm text-muted">Browsers that skip the device approval step. They never skip your password.</p>
        {browsers.length === 0 ? (
          <p className="text-sm text-muted">No trusted browsers.</p>
        ) : (
          <ul className="divide-y divide-border rounded-lg border border-border">
            {browsers.map((b) => (
              <li key={b.id} className="flex items-center justify-between gap-4 p-3">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">
                    {[b.browser, b.operatingSystem].filter(Boolean).join(" · ") || b.label || "Browser"}
                    {b.isCurrent ? <span className="ml-2 text-xs text-muted">(this browser)</span> : null}
                  </p>
                  <p className="text-xs text-muted">
                    {[b.approxLocation ?? b.ip, b.lastUsedAt ? `last used ${relativeTime(b.lastUsedAt)}` : `added ${relativeTime(b.createdAt)}`]
                      .filter(Boolean)
                      .join(" · ")}
                  </p>
                </div>
                <Button type="button" variant="secondary" disabled={busy === b.id} onClick={() => removeBrowser(b.id, "this browser")}>
                  Remove
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* 6 — Active sessions */}
      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Where you&apos;re signed in</h2>
          {sessions.length > 0 ? (
            <Button type="button" variant="secondary" disabled={busy === "signout"} onClick={signOutAll}>
              {busy === "signout" ? "Signing out…" : "Sign out everywhere"}
            </Button>
          ) : null}
        </div>
        {sessions.length === 0 ? <p className="text-sm text-muted">No other active sessions.</p> : null}
        <ul className="divide-y divide-border rounded-lg border border-border">
          {sessions.map((s) => (
            <li key={s.id} className="flex items-center justify-between gap-4 p-3">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium">
                  {s.deviceName ?? "Unknown device"}
                  {s.isCurrent ? <span className="ml-2 text-xs text-muted">(this device)</span> : null}
                </p>
                <p className="text-xs text-muted">{s.platform ?? "unknown"} · started {relativeTime(s.createdAt)}</p>
              </div>
              <Button type="button" variant="secondary" disabled={busy === s.id} onClick={() => run(s.id, () => revokeSession(accessToken, s.id), "Signed out of that session.")}>
                Sign out
              </Button>
            </li>
          ))}
        </ul>
      </section>

      {/* 7 — Recovery codes */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Recovery codes</h2>
        <p className="text-sm text-muted">
          One-time codes to get back in if you lose every device. {remaining !== null ? `${remaining} unused.` : ""}
        </p>
        {freshCodes ? (
          <div className="space-y-3 rounded-lg border border-border p-4">
            <p className="text-sm font-medium">Save these now — they won&apos;t be shown again. Generating them revoked any previous set.</p>
            <ul className="grid grid-cols-2 gap-2 font-mono text-sm">
              {freshCodes.map((c) => (
                <li key={c}>{c}</li>
              ))}
            </ul>
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="secondary" onClick={() => run("copy", async () => void (await navigator.clipboard.writeText(freshCodes.join("\n"))), "Copied.")}>
                Copy
              </Button>
              <Button type="button" variant="secondary" onClick={() => downloadCodes(freshCodes)}>
                Download
              </Button>
              <Button type="button" onClick={() => setFreshCodes(null)}>
                I&apos;ve saved them
              </Button>
            </div>
          </div>
        ) : stepUp ? (
          <StepUpRequired
            accessToken={accessToken}
            reason="Generating recovery codes is a high-security action, so we need a fresh confirmation from a trusted device."
            onCancel={() => setStepUp(false)}
            onSatisfied={async () => {
              await makeRecoveryCodes();
            }}
          />
        ) : (
          <Button type="button" variant="secondary" onClick={makeRecoveryCodes} disabled={busy === "codes"}>
            {busy === "codes" ? "Generating…" : remaining ? "Replace recovery codes" : "Generate recovery codes"}
          </Button>
        )}
      </section>

      {/* 8 — Security activity */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold">Recent security activity</h2>
        {activity.length === 0 ? (
          <p className="text-sm text-muted">Nothing recent.</p>
        ) : (
          <ul className="divide-y divide-border rounded-lg border border-border">
            {activity.map((item, index) => (
              <li key={`${item.type}-${item.created_at}-${index}`} className="flex items-center gap-3 p-3">
                <span className={`h-2 w-2 shrink-0 rounded-full ${severityDot(item.severity)}`} aria-hidden />
                <span className="flex-1 truncate text-sm">{securityActivityLabel(item.type)}</span>
                <span className="shrink-0 text-xs text-muted">{relativeTime(item.created_at)}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* 9 — Account recovery */}
      <section className="space-y-2">
        <h2 className="text-lg font-semibold">Lost access?</h2>
        <p className="text-sm text-muted">
          If you can&apos;t sign in and have lost your devices, use your recovery codes to get back in.{" "}
          <a href="/recover" className="underline">Recover your account</a>.
        </p>
      </section>
    </div>
  );
}

function Stat({ label, value, ok }: { label: string; value: string; ok: boolean }) {
  return (
    <div className="rounded-md border border-border bg-surface p-3">
      <p className="text-xs text-muted">{label}</p>
      <p className={`mt-0.5 text-sm font-semibold ${ok ? "text-text" : "text-danger"}`}>{value}</p>
    </div>
  );
}

function severityDot(severity: string): string {
  switch (severity) {
    case "critical":
      return "bg-danger";
    case "warning":
      return "bg-warning";
    default:
      return "bg-accent";
  }
}

function ItemList({
  items,
  empty,
  busyId,
  onRemove
}: {
  items: DeviceView[];
  empty: string;
  busyId: string;
  onRemove: (d: DeviceView) => void;
}) {
  if (items.length === 0) return <p className="text-sm text-muted">{empty}</p>;
  return (
    <ul className="divide-y divide-border rounded-lg border border-border">
      {items.map((d) => (
        <li key={d.id} className="flex items-center justify-between gap-4 p-3">
          <div className="min-w-0">
            <p className="truncate text-sm font-medium">{d.name ?? "Unnamed"}</p>
            <p className="text-xs text-muted">
              {d.platform} · {d.state} · added {relativeTime(d.createdAt)}
            </p>
          </div>
          <Button type="button" variant="secondary" disabled={busyId === d.id} onClick={() => onRemove(d)}>
            Remove
          </Button>
        </li>
      ))}
    </ul>
  );
}

/** A human-recognisable default so the device list isn't a wall of "Unnamed". */
function defaultPasskeyName(): string {
  if (typeof navigator === "undefined") return "Passkey";
  const ua = navigator.userAgent;
  if (/iPhone|iPad/.test(ua)) return "iPhone passkey";
  if (/Android/.test(ua)) return "Android passkey";
  if (/Mac/.test(ua)) return "Mac passkey";
  if (/Windows/.test(ua)) return "Windows passkey";
  return "Passkey";
}
