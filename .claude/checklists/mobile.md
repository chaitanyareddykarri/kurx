# Checklist: Mobile

- [ ] Feature-first Clean Architecture respected: `presentation → domain ← data`, `domain` framework-free, DTOs mapped to entities before reaching UI (D-019)
- [ ] Only D-019-approved backend surface consumed — no invented API/DTO/field; camelCase out, snake_case in
- [ ] Token in `flutter_secure_storage` only — never SharedPreferences/Hive/plaintext, never in a log line or query string
- [ ] API base URL from build config (`KURX_API_BASE`), no stale port fallback (the D-017 bug class)
- [ ] RFC7807 ProblemDetails parsed to the one `ApiError` shape and shown, not swallowed; `401`→refresh handled
- [ ] Money rendered as ₹ from `long` paise via the shared helper (D-004), formatted only at the display edge
- [ ] Loading / error / empty / retry state present on every data-driven screen (shared pattern, not reinvented)
- [ ] `flutter analyze` clean + `flutter test` green; test count reported before → after
- [ ] Flow exercised on a real emulator/device against the **running** backend (not assumed from types)
- [ ] No unrequested scope (no payments/tickets/check-in/wallet/organizer/notifications — backend absent per D-019)
- [ ] No secret/`.env`/hardcoded token committed; no file >~300 lines without justification; no TODO/placeholder left in
