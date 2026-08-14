# Kurx Mobile

> **Architecture: event-first ([D-074](../docs/DECISIONS.md)).** No organizer/owner accounts. The organizer feature area reflects the legacy D-055 org-first model, pending migration; see [`../docs/architecture/event-creation.md`](../docs/architecture/event-creation.md).

Flutter attendee app for Kurx (D-019). Feature-first clean architecture (`lib/features/*/{data,domain,presentation}`), Riverpod + GoRouter + Dio + Freezed + Hive. Screens exist for auth, discovery/search, event detail, orders, event chat, certificates, gamification, calendar, profile, settings, and organizer flows — wiring depth varies per screen; build-status authority is [`docs/roadmap/README.md`](../docs/roadmap/README.md).

## Setup

Requires Flutter 3.x, Dart SDK, Android SDK or Xcode.

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

- WhatsApp OTP login → JWT + secure token storage (Keychain/Keystore) with silent refresh
- Guest mode — attendees browse without logging in; only onboarding requires a session
- Discovery home (featured/trending/upcoming/latest), category chips, calendar
- Event search with keyword, city, category, date-range filters, and infinite scroll pagination
- Event detail with ticket types (₹ paise pricing + availability), related events
- Data layers for orders, certificates, and gamification (`features/*/data/`)
- Event chat (D-108): SignalR client (`signalr_netcore`) with attachments and presence (`features/social/`)
- Design system: `KurxColors` ThemeExtension (light + dark), shared widget library

## What's not built yet

- Payment checkout — gated on the D-070 security stop; no payment code until that clears
- Data layers for `notifications/`, `bookmarks/` (no data layer today)
- Check-in scanning, wallet, push-notification registration

## Testing

```bash
flutter analyze
flutter test          # hermetic suite in test/ (no backend required)
```

Live integration tests against a running backend live in `test_live/` and are run manually, not by `flutter test`.
