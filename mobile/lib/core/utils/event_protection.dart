/// D-388 — is this event's substance frozen behind admin approval?
///
/// The rule is **Public product AND publicly live**, and both halves matter. A Private event is never
/// reviewed — its host is its only audience — so it stays directly editable at every status. A Public
/// event still in `draft` or `approved` is not public yet: nobody has seen it, it can hold no orders,
/// and D-363 §4 already handles an edit there by returning it to the review queue.
///
/// **Mirrors the server's `EventStatusWorkflow.IsLiveProtected` and web's `lib/event-protection.ts`,
/// and must move with both.** The server is the enforcement; a client that disagrees either offers an
/// action the API refuses or hides one the host is entitled to. Same discipline as `openHostStates`:
/// two clients disagreeing about a server rule is one defect written twice.
library;

const Set<String> liveProtectedStates = {'published', 'scheduled', 'live'};

bool liveProtected(String product, String status) =>
    product.toLowerCase() == 'public' &&
    liveProtectedStates.contains(
        status.toLowerCase().replaceAll(RegExp(r'[\s_-]'), ''));
