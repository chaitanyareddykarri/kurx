# Checklist: Backend

- [ ] `DOTNET_ROOT` exported, `dotnet build` clean
- [ ] Request DTOs have FluentValidation + `WithValidation<T>()`
- [ ] No EF/persistence types referenced from `Kurx.Api` handlers directly
- [ ] Authz: `KurxAdmin` policy or live resource-role check applied as appropriate
- [ ] Errors surface as RFC7807 `ProblemDetails`, not raw exceptions
- [ ] Integration test added/updated in `Kurx.Tests` against real `kurx_test`
- [ ] `dotnet test` green, count reported before → after
- [ ] New endpoint smoke-tested live (curl or equivalent)
- [ ] `docs/api/README.md` updated if public contract changed
