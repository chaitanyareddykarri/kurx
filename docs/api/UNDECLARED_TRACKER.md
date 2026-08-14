# Undeclared response tracker

<!-- GENERATED — do not edit by hand.
     Regenerate: node scripts/openapi-response-check.mjs --update && node scripts/tracker.mjs -->

Every operation in `docs/api/openapi.json` whose success response carries no schema, tracked to
closure. Generated from the spec, the allow-list and `scripts/openapi-response-check.mjs`, so it
cannot drift from what CI actually enforces ([D-313](../DECISIONS.md)).

**An undeclared response is not merely undocumented — it is invisible to `contract-check.mjs`**, which
then has nothing to validate the three hand-written client model sets against and passes vacuously. That
is the D-303 failure mode, and it is why this list exists rather than a "someday" note.

## Status

| | Count |
|---|---:|
| Total operations | **518** |
| Declared | **480** |
| Undeclared — intentional | **4** |
| Undeclared — pending | **34** |

The ratchet (`scripts/openapi-response-check.mjs`, CI-enforced) fails on any *new* undeclared response
and on a *stale* allow-list entry, so **pending can only decrease**.

## Intentional — never to be "fixed"

Each is a decision with a reason recorded in `scripts/openapi-response-check.mjs`. Declaring any of
them would misdescribe the wire.

| Operation | Why |
|---|---|
| `POST /v1/webhooks/razorpay` | Empty 200 ack — Razorpay reads only the status; there is no body. |
| `POST /v1/webhooks/whatsapp` | Empty 200 ack — Meta treats any 2xx as delivered. |
| `PUT /v1/storage/{key}` | Local-disk presigned receiver; mapped only when `STORAGE_PROVIDER=localdisk`, so it does not exist in any real deployment (D-110). |
| `POST /v1/gate/{eventId}/scan` | **Polymorphic 200** — a duplicate scan and a successful scan return different shapes. OpenAPI 3.0 allows one schema per status code, so any declaration is wrong half the time. A union would emit nulls the wire never sends; 409 would change status semantics for every scanner. |

## Pending — 34 operations

The live list is `docs/api/undeclared-allowlist.json`; this is its readable form.

Most remaining shapes are **singletons** — one small record each, no shared pattern left to exploit.
When closing one, follow the ladder in [`.claude/memory/api-conventions.md`](../../.claude/memory/api-conventions.md):
reuse an existing contract first, and **declare the record in `Kurx.Application.Abstractions`** or it
silently serializes camelCase.

### admin — 8

- `DELETE /v1/admin/staff/{userId}/roles/{role}`
- `GET /v1/admin/orgs/pending`
- `GET /v1/admin/staff`
- `GET /v1/admin/verification-documents/{documentId}/view`
- `GET /v1/admin/verifications/{subjectType}/{subjectId}/history`
- `POST /v1/admin/notifications/broadcast`
- `POST /v1/admin/phone-backfill`
- `POST /v1/admin/staff/grant`

### auth — 7

- `POST /v1/auth/devices/enroll`
- `POST /v1/auth/devices/enroll/verify`
- `POST /v1/auth/login/password`
- `POST /v1/auth/login/status`
- `POST /v1/auth/passkeys/login/options`
- `POST /v1/auth/passkeys/register`
- `POST /v1/auth/passkeys/register/options`

### events — 6

- `GET /v1/events/{eventId}/certificates`
- `GET /v1/events/{eventId}/invitations`
- `GET /v1/events/{eventId}/reviews`
- `POST /v1/events/{eventId}/certificates/generate`
- `POST /v1/events/{eventId}/invitations/send`
- `POST /v1/events/{eventId}/reviews`

### me — 3

- `GET /v1/me/certificates`
- `PATCH /v1/me/profile`
- `POST /v1/me/email/change/start`

### orgs — 2

- `GET /v1/orgs/{orgId}/kyc`
- `GET /v1/orgs/{orgId}/workspace-capabilities`

### public — 2

- `GET /v1/public/orgs/{slug}`
- `POST /v1/public/invitations/{token}/rsvp`

### stages — 2

- `POST /v1/stages/{stageId}/advance`
- `POST /v1/stages/{stageId}/participants/seed-registered`

### certificates — 1

- `GET /v1/certificates/{code}`

### usernames — 1

- `GET /v1/usernames/availability`

### users — 1

- `GET /v1/users/search`

### teams — 1

- `POST /v1/teams/{teamId}/split`

## How to close one

1. Read the endpoint and determine the **actual** runtime shape — do not infer it from the route name.
2. Search for an existing contract to reuse. Many views are already exact 1:1 projections.
3. Only then add the smallest record, in `Kurx.Application.Abstractions`.
4. Add `.Produces<T>()`.
5. Regenerate the spec and **diff it** — `.Produces<T>()` is metadata the C# compiler never checks, so
   this is the only step that catches a wrong declaration.
6. `node scripts/openapi-response-check.mjs --update` to drop it from the list, then regenerate this file.

**Two traps that cost real time:** a mapper that *translates* (57 `.ToLowerInvariant()` calls across the
API turn `"Published"` into the `"published"` the wire carries) must keep its translation; and an
envelope (`{items, total}`) must never be declared as its element type.
