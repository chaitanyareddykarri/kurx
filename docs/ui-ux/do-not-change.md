# Kurx UI/UX Redesign — Frozen Surfaces

Established in Phase 0.3. **Nothing in this file may be edited by a redesign phase.** If a redesign
genuinely requires a change here, that is a blocker: stop, record it in the Regression Ledger of
`UI_REDESIGN_PROGRESS.md`, open a `D-NNN` in `docs/DECISIONS.md`, and get approval first.

## 1. Backend — entirely frozen

`backend/**` — the redesign is client-side. `.claude/CLAUDE.md` §3 forbids changing backend behavior
to simplify frontend work, and the roadmap repeats it.

## 2. API contract artifacts

| Path | Why |
|---|---|
| `docs/api/openapi.json` | CI's contract-drift gate diffs the live spec against this file. A stale or edited copy reddens every build. |
| `web/lib/api.ts` — Zod schemas | Hand-maintained mirrors of backend DTOs. Editing a schema to make a component compile is how the wallet `snake_case` outage happened (D-259). |
| `admin/lib/api.ts` — Zod schemas | Same. |
| `mobile/lib/**/data/models/*.dart` | Same, plus their generated companions below. |

Rendering may change freely. The **shape** being parsed may not.

## 3. Generated code

* `mobile/lib/**/*.freezed.dart`
* `mobile/lib/**/*.g.dart`

22 generated files are tracked and `build_runner` deliberately does **not** run in CI — a hand-edit
shows up as an analyze failure. Change the source model and regenerate; never edit output.

## 4. Auth, session and transport

| Path | Why |
|---|---|
| `web/lib/session.ts`, `admin/lib/session.ts` | httpOnly cookie handling; route-group auth enforcement. Was found dead once and silently passing everyone. |
| `web/lib/auth-api.ts`, `web/lib/webauthn.ts` | Login/OTP/passkey wire calls. |
| `mobile/lib/core/network/**` | Interceptors, retry, `guard()` → `ApiError` conversion (D-064, D-093). |
| `mobile/lib/core/session/**`, `mobile/lib/core/security/**` | Token store, device keys, passkeys. |
| `web/app/api/realtime-token/route.ts`, `admin/app/api/realtime-token/route.ts` | SignalR token mint. |

Phases 16 / 36 restyle these screens. They do not touch the logic beneath them.

## 5. Real-time

`admin/lib/use-admin-event-hub.ts`, `web/lib/use-chat-hub.ts`, `mobile/lib/features/social/data/datasources/chat_hub_client.dart`
— the SignalR group-join membership re-check is a security invariant (D-017). Phase 31 restyles
connection **indicators**, not connection **behavior**.

## 6. Build and CI configuration

* `.github/workflows/**`
* `backend/**/*.csproj`, `Kurx.sln`
* `mobile/pubspec.yaml`, `mobile/pubspec.lock` (except a dependency explicitly approved by a `D-NNN`)
* `next.config.*`, `postcss.config.*` in `web/` and `admin/`

`tailwind.config.*` is **not** frozen — Phase 4.0 is required to fix its duplication.

## 7. Secrets and environment

`.env`, `.env.local`, `.env.example`, `backend/**/appsettings*.json`, `infra/**`. Never staged,
never edited by a redesign phase.

## 8. Pre-existing dirty working tree (quarantined)

These were already modified/untracked at baseline commit `c66ae16` and are **unrelated to the
redesign**. They must never be staged with a redesign commit:

```
 M .env.example
 M README.md
 M backend/Kurx.Api/appsettings.Development.json
 M mobile/analysis_options.yaml
 M mobile/ios/Flutter/Debug.xcconfig
 M mobile/ios/Flutter/Release.xcconfig
 M mobile/macos/Flutter/Flutter-Debug.xcconfig
 M mobile/macos/Flutter/Flutter-Release.xcconfig
?? mobile/ios/Podfile
?? mobile/macos/Podfile
```

Every redesign commit stages explicit paths. `git add -A` and `git commit -a` are forbidden for the
duration of this program.

## 9. Governing documents — append only, never rewrite

* `docs/DECISIONS.md` — append new `D-NNN` entries; never edit or delete an existing one.
* `docs/architecture/TERMINOLOGY.md` — canonical vocabulary (D-271). Phase 43 audits copy *against*
  it; it is not a place to invent new words.
* `.claude/CLAUDE.md` and `.claude/memory/*.md` — updated only via the change-trigger matrix, and
  only for conventions the redesign actually changes.
