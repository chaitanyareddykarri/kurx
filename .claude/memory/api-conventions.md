# API Conventions

Full contract reference: `docs/api/README.md`. This file is the short-context summary of the *rules*, not the endpoint list.

## Request/response

- Every request DTO validated by FluentValidation via `WithValidation<T>()` endpoint filter before the handler runs.
- Every error response — validation failure, expected business error (403/404/409/...), unhandled exception — is RFC7807 `ProblemDetails`. Never return a raw string or ad-hoc error JSON shape.
- Every request carries/echoes `X-Correlation-Id`, enriched into structured logs (`CorrelationId`/`UserId`/`OrgId`).

## Auth

- JWT bearer for HTTP. SignalR hubs (`ScanHub`, `SalesHub`) take the JWT via `?access_token=` query string (browsers can't set headers on WS/SSE) and re-check org/event membership on group join — never trust a client-supplied org/event id without that check.
- `KurxAdmin` policy for claim-based platform-admin gates; resource-specific role checks (Owner/Manager/Staff/Finance) for anything scoped to an org/event, queried live from Postgres, not cached in the token.

## Public vs. authenticated surfaces

Public discovery endpoints (`/v1/events` search/upcoming/trending/featured/latest/detail/related) must stay read-only and use real data (e.g. `ViewCount` for trending) — never fabricate/stub metrics on a public endpoint.

## Response casing — camelCase in, snake_case out (D-259)

**The contract is `camelCase` request bodies, `snake_case` responses.** `Results.Ok(someRecord)` used to
silently opt out and emit camelCase (Minimal API's `JsonSerializerDefaults.Web`), which broke clients at
least three times — `GET /v1/orgs/{id}/wallet` (D-064), `AttendeeRow` (D-208), all of
`GamificationEndpoints.cs` (D-210/D-212). That is fixed: `SnakeCaseResponseConverter` renames on write,
and `SnakeCaseResponseSchemaFilter` makes the published spec say the same thing.

Three things to know before relying on it:

- **It is scoped by NAMESPACE, not by naming convention.** The converter applies to types declared in
  `Kurx.Application.Abstractions`. A response record declared anywhere else still serializes camelCase,
  **with a green build and passing tests** — nothing catches it. **Declare response types in
  `Abstractions`.** (This used to cite `DevCapabilityPreview` in `Kurx.Infrastructure.Dev` as the live
  example; Developer Mode was deleted wholesale in [D-274](../../docs/DECISIONS.md) and that type no
  longer exists. The rule is unchanged — only the example was dead.)
- **It is write-only, deliberately.** `PropertyNamingPolicy` would have governed deserialization too and
  demanded snake_case request bodies from every deployed client. Request binding is untouched.
- **Anything not on the HTTP response path is unaffected** — stored payloads, outbox rows and push
  `DataJson` keep whatever keys their producer wrote. `ChatNotificationJob` emits `notificationType`.
  Do not "fix" those to snake_case; they are not responses.

For .NET consumers (i.e. the test suite) `ReadFromJsonAsync<T>` is case-insensitive but **not**
separator-insensitive, so it cannot map `fcm_token` → `FcmToken` — and it does not throw, it leaves
defaults. Use `TestJson.ReadAsync<T>` instead; reading into `JsonElement` needs nothing.

**Declare the success response** ([D-313](../../docs/DECISIONS.md)). 439 of 518 operations carry a schema,
32 declare `204`, 6 declare binary. An undeclared response is a gap, not the norm — and **CI enforces it**:
`scripts/openapi-response-check.mjs` fails a new endpoint that does not say what it returns. If you are
adding one, declare it or the build breaks.

Pick the cheapest rung that is honest:

1. Endpoint already returns a `Kurx.Application.Abstractions` type → `.Produces<T>()`. No new type.
2. Its `To*Json` mapper is a 1:1 snake_case projection of its View → delete the mapper, return the View.
3. The mapper *translates* → a named record carrying the translation verbatim.
4. The shape already exists elsewhere → reuse it. `new { ok = true }` is `OperationAck`, shared by 80
   endpoints; never add a per-endpoint sibling.
5. `204` / empty `200` / binary → declare what it is. Do not invent a DTO.

**Three rules that break the wire silently if ignored:**

* **A response DTO must live in `Kurx.Application.Abstractions`.** `SnakeCaseResponseConverter` selects
  by namespace; a record declared beside its endpoint emits camelCase, and the build stays green.
* **A mapper is not boilerplate.** 26 of 48 mappers *translate* — 57 `.ToLowerInvariant()` calls turn
  `"Published"` into the `"published"` the wire has always carried, plus renames like `org_id ←
  RepresentingOrgId`. Diff key-by-key **and value-by-value** before replacing one, in both directions: a
  mapper that omits a View property is not 1:1 either, and returning the View would add fields.
* **Adding a field to a `*View` is not finished until the wire record and its mapper carry it too**
  ([D-326](../../docs/DECISIONS.md)). `CategoryView.ProductClass` was computed, stored, projected — and
  dropped by a seven-parameter `ToJson` into a `CategoryResponse` with no slot for it, so
  `/v1/categories` served **0 of 111** types with `product_class` and the Create-Event wizard's Private
  branch offered no categories at all. Nothing failed: the clients declared the field `.optional()`
  (correct — that is how a client survives an older backend, and it also makes *missing* and
  *unclassified* indistinguishable), and the drift gate proves the spec matches the **mapper**, never
  that the mapper matches the **view**. There is no type error to catch this and no gate that can see
  it, in either direction — D-245 was the same seam failing the other way. **Grep the `To*Json` for the
  property name before calling a View change done, and prove it over HTTP**: every one of the nine
  existing `ProductClass` assertions read the entity, which is precisely why all of them passed.

## Adding an endpoint

1. Interface method in `Kurx.Application.Abstractions`.
2. Implementation in the matching `Kurx.Infrastructure` folder.
3. Endpoint in `Kurx.Api/Endpoints`, DTO with a FluentValidation validator, `WithValidation<T>()` applied.
4. Integration test in `Kurx.Tests` against real `kurx_test`.
5. Update `docs/api/README.md` if the public contract changed.
