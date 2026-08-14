/**
 * Maps the backend's `security_events` type codes to friendly copy — kept in lock-step with the web
 * `lib/security-activity.ts`. Unknown types are humanized rather than dropped.
 */
const LABELS: Record<string, string> = {
  "login.succeeded": "Signed in",
  "login.approved": "Sign-in approved on a device",
  "login.rejected": "Sign-in declined on a device",
  "login.password_browser": "Signed in with password on a trusted browser",
  "password.created": "Password created",
  "password.changed": "Password changed",
  "password.reset": "Password reset",
  "password.locked_out": "Account locked after failed sign-ins",
  "email.verified": "Email verified",
  "passkey.registered": "Passkey added",
  "passkey.registration_failed": "Passkey setup failed",
  "passkey.assertion_failed": "Passkey sign-in failed",
  "recovery.codes_generated": "Recovery codes generated",
  "recovery.code_rejected": "Recovery code rejected",
  "refresh.reuse_detected": "Suspicious session reuse blocked",
  "refresh.pop_failed": "Session security check failed",
  "risk.denied": "Sign-in blocked by risk checks",
  "challenge.approved": "Device challenge approved",
  "challenge.signature_invalid": "Invalid device signature",
  "challenge.match_number_invalid": "Wrong match number entered",
  "challenge.match_attempts_exhausted": "Too many match-number attempts",
  "otp.issued": "One-time code sent",
  "otp.verified": "One-time code verified"
};

export function securityActivityLabel(type: string): string {
  return LABELS[type] ?? type.replace(/[._]/g, " ").replace(/^\w/, (c) => c.toUpperCase());
}

export function relativeTime(iso: string): string {
  const secs = Math.round((Date.now() - new Date(iso).getTime()) / 1000);
  if (secs < 60) return "just now";
  const mins = Math.round(secs / 60);
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.round(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  if (days < 30) return `${days}d ago`;
  return new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });
}
