# Template: API Endpoint

Fill in when adding an endpoint, per `.claude/memory/api-conventions.md`.

**Route**: `METHOD /v1/...`
**Auth**: public / JWT / `KurxAdmin` / resource-role (specify which)
**Request DTO**: fields + FluentValidation rules
**Response shape**: success + error (RFC7807) cases
**Application interface**: method added to which `Kurx.Application.Abstractions` interface
**Test**: integration test class/method covering it
**Docs**: `docs/api/README.md` section to update
