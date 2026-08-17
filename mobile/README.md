# Kurx Mobile

> **Architecture: user-first and event-first ([D-267](../docs/DECISIONS.md)/[D-268](../docs/DECISIONS.md), building on [D-074](../docs/DECISIONS.md)).** No organizer/owner accounts and no personal organizations — a **User** owns an event; the organization it *represents* is a field inside the creation flow. **The migration this note used to call "pending" is done:** the org-first navigation is gone from Flutter, there is no `currentOrg`/org-switcher state, and `organizer/.../representing_page.dart` is what replaced the old *My Organizations* door. See [`../docs/architecture/event-creation.md`](../docs/architecture/event-creation.md) and [`../docs/architecture/TERMINOLOGY.md`](../docs/architecture/TERMINOLOGY.md).

Flutter attendee app for Kurx (D-019). Feature-first clean architecture (`lib/features/*/{data,domain,presentation}`), Riverpod + GoRouter + Dio + Freezed + Hive. Screens exist for auth, discovery/search, event detail, orders, event chat, certificates, gamification, calendar, profile, settings, and organizer flows — wiring depth varies per screen; build-status authority is [`docs/roadmap/README.md`](../docs/roadmap/README.md).

## Setup

Requires **Flutter 3.44.6** (the version CI pins — `.github/workflows/ci.yml`), Dart SDK ≥ 3.12.2,
and the Android SDK or Xcode.

```bash
cd mobile
flutter pub get
flutter run
```

The app connects to the Kurx backend at `http://localhost:5080` in dev — the same port `dotnet run` and
`docker compose up` both serve.

**On a device, `localhost` is the device.** Open a USB tunnel once per connection and the default works
unchanged, on a physical phone and an emulator alike:

```bash
adb reverse tcp:5080 tcp:5080     # API
adb reverse tcp:3000 tcp:3000     # web, only if you open in-app links to it
adb reverse --list                # confirm
```

Symptom when it is missing: every request fails as **"Request timed out"** with the backend perfectly
healthy on the host. The tunnel does not survive a replug or an `adb kill-server` — re-run it.

For staging or production, pass the origin explicitly; it is used verbatim, with no rewriting and no
fallback (D-017):

```bash
flutter run --dart-define=KURX_API_BASE=https://api.example.com
```

Config lives in `lib/core/network/app_config.dart`.

## What's built

- Phone OTP login **over SMS** (D-281 — WhatsApp is never used for auth codes) → JWT + secure token storage (Keychain/Keystore) with silent refresh
- Guest mode — attendees browse without logging in; only onboarding requires a session
- Discovery home (featured/trending/upcoming/latest), category chips, calendar
- Event search with keyword, city, category, date-range filters, and infinite scroll pagination
- Event detail with ticket types (₹ paise pricing + availability), related events
- Data layers for orders, certificates, and gamification (`features/*/data/`)
- Event chat (D-108): SignalR client (`signalr_netcore`) with attachments and presence (`features/social/`)
- Design system: `KurxColors` ThemeExtension (light + dark), shared widget library
- **Saved events and notifications are server-backed** — `bookmarks/` reconciles a Hive cache against
  `GET /v1/me/saved` (so a star set on the phone shows up on the website), `notifications/` has a
  real remote data source
- **Push registration** — `core/push/push_service.dart` requests an FCM token and `POST`s it to
  `/v1/devices/register`; `firebase_core`/`firebase_messaging` are dependencies and
  `android/app/google-services.json` is committed
- **Organizer surface — 24 of the 95 screens**, including the check-in scanner
  (`organizer/.../checkin_scanner_page.dart`), attendees, announcements, certificates, the org
  wallet (`org_wallet_page.dart`) and team/staff assignment
- Auth beyond OTP: trusted devices, passkeys, recovery codes, step-up, device-approved sign-in

## What's not built yet

*Re-verified 2026-08-15. Every bullet this section previously carried had been closed — it named
`notifications`/`bookmarks` as having no data layer, and check-in scanning, wallet and push
registration as unbuilt. All five exist; see "What's built" above.*

- **Paid checkout cannot complete** — not for want of client code. `checkout_page.dart` is a real
  screen: it creates the order, and when the server answers `payments_not_enabled` or
  `paid_group_not_supported_yet` it says so instead of fabricating a payment step. The blocker is
  the backend running `PAYMENT_PROVIDER=mock` (D-070), so no gateway hand-off exists to build
  against. Free and guest tickets book end-to-end today.
- **iOS push** — `google-services.json` is committed for Android but there is no
  `GoogleService-Info.plist`, so only Android can receive a notification.
- **Trust endpoints** — identity/org verification, membership claims and capability reads are not
  yet wired into the client. Tracked per screen in [`docs/roadmap/README.md`](../docs/roadmap/README.md).
- **Offline gate mode** — the check-in scanner requires connectivity; offline replay is a
  backend-supported feature (D-266 M13) with no client yet.

## Testing

```bash
flutter analyze
flutter test          # hermetic suite in test/ (no backend required)
```

Live integration tests against a running backend live in `test_live/` and are run manually, not by `flutter test`.
