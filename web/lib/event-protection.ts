/// D-388 — is this event's substance frozen behind admin approval?
///
/// The rule is **Public product AND publicly live**, and both halves matter. A Private event is never
/// reviewed — its host is its only audience — so it stays directly editable at every status. A Public
/// event that is still `draft` or `approved` is not public yet: nobody has seen it, it can hold no
/// orders, and D-363 §4 already handles an edit there by returning it to the review queue.
///
/// **This mirrors the server's `EventStatusWorkflow.IsLiveProtected` and must move with it.** The server
/// is the enforcement — a client that got this wrong would offer a button the API then refuses, or hide
/// one the host is entitled to. Same discipline as `OPEN_HOST_STATES`: two clients disagreeing about a
/// server rule is one defect written twice.
export const LIVE_PROTECTED_STATES = new Set(["published", "scheduled", "live"]);

export function liveProtected(product: string, status: string): boolean {
  return product.toLowerCase() === "public" && LIVE_PROTECTED_STATES.has(status.toLowerCase());
}
