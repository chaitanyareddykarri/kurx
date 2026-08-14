# Phase 10: Mobile App & Production Readiness

**Status: in progress** — first attendee slice shipped; capstone (payments/tickets/check-in live, full-stack production pass) still pending. Not complete.

## Progress

- **Slice 1 (D-019), verified live:** Flutter attendee app in `mobile/` — WhatsApp OTP login, secure token storage (Keychain/Keystore) with silent `401`→refresh, `/v1/me` onboarding detection, and public event browse (upcoming list). Feature-first Clean Architecture (Riverpod/GoRouter/Dio/Freezed/Hive/Material 3). Web target compiles.
- **Slice 2 (D-019), verified live:** event detail screen — `GET /v1/events/{slug}` (title/subtitle/description/venue/date/tags), read-only public ticket types (`GET /v1/events/{eventId}/ticket-types`, ₹/paise pricing + availability), and related events (`/related`). Tap-through from the list; back-navigation.
- **Slice 3 (D-019), verified live:** event search & filters — `GET /v1/events` with text search (debounced), city + category (`/v1/categories`) + date-range filters, sort (soonest/`date_desc`/`newest`/`popular`), and `{items,total}` pagination via infinite scroll + a Load-more fallback. Full loading/empty/error/retry states incl. a load-more retry that preserves loaded items.
- **Slice 4 (D-019), verified live:** public browse without login. App boots into guest mode (initial route `/events`); the router guard no longer walls unauthenticated users — only `/onboarding` requires a session. Guests browse/search/filter/detail/related/ticket-types freely (all public endpoints, no token → no `Authorization` header). Non-intrusive guest banner + Sign-in CTA on the home app bar; `loginReturnToProvider` returns the user to their origin screen after auth (no lost navigation). Logout lands on guest browse, not a wall. Security boundary verified live: a tokenless `/v1/me` is rejected (401 → `ApiError`).
- **Slice 5 (D-019), verified live:** discovery home dashboard replaces the plain landing list. `GetHomeFeed` fetches `/v1/events/featured|trending|latest|upcoming` + `/v1/categories` concurrently (`Future.wait`) into one `HomeFeed`; `homeFeedProvider` drives skeleton loading, empty, and error/retry with pull-to-refresh. Sections: tappable search bar, Featured carousel (`PageView`), Trending/Upcoming/Latest horizontal rows (lazy `ListView.builder`) each with a "View all" → `/discover/:section` list, and Browse-categories chips → the search screen pre-filtered by category. Guest + authenticated users share the same discovery experience. Material 3, dark/light, accessible (Semantics on cards/search). Old upcoming-only list page retired.
- **Verification across all slices:** `flutter analyze` clean; 29 hermetic tests green; live backend integration tests (real datasources/DTOs vs running API on :5080) green, including guest-vs-protected boundary and full discovery-feed tests.
- **Not done (unchanged):** payments, ticket purchase/orders, tickets/QR, check-in scanning (ScanHub has no scan endpoint), wallet, notifications, organizer tools, and the production-readiness capstone — each blocked on unbuilt backend.
- **Environment limitation logged:** GUI run on a mobile emulator/device was not possible in the build environment (no Android SDK; Xcode/CocoaPods incomplete for iOS/macOS). Live verification was done via the real-network integration test against the running backend instead.

## Objective

Ship the Flutter mobile app (attendee + scanning use cases) and close remaining production-readiness gaps across the platform.

## Deliverables

Flutter app consuming the existing public/auth API · mobile check-in scanning against `ScanHub` · final production-readiness pass: real providers fully live (Phase 8), payments/payouts live (Phases 4/6), load/security hardening pass across the whole stack.

## Dependencies

Phases 1–9 substantially complete — this is the capstone phase.

## Completion criteria

Mobile app functional against the live API for its core flows; a full `.claude/workflows/release.md` pass across the entire stack is green with no phase left with an open "not built yet" item from `docs/roadmap/README.md`.

## Verification

All `.claude/checklists/` files, full `.claude/reviews/` pass recommended before calling the platform production-ready.

## Loop OS integration

- **Acceptance criteria (testable):** the Flutter app performs its core flows (attendee browse/auth, mobile check-in via `ScanHub`) against the live API; a full-stack release pass is green; no phase retains an open "not built yet" item in `docs/roadmap/README.md`.
- **Exit criteria:** mobile app functional against live API, every prior phase closed, full release + reviews pass, roadmap shows nothing outstanding, semantic release tagging established (`versioning.md`).
- **Required reviews:** **all** `.claude/reviews/` (architecture, backend, frontend, api, database, security, performance, accessibility, testing, documentation, code-quality) — this is the production-readiness capstone.
- **Required documentation:** `D-NNN` for mobile + production posture; `docs/deployment/` production runbook; `docs/architecture/overview.md` final; `docs/roadmap/README.md` cleared of open items.
