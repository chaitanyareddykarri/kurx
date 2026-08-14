# Review Template: Architecture

Use for structural changes spanning layers, or before starting a new phase.

## Check against

- `.claude/memory/architecture.md` / `docs/architecture/overview.md` layering rule (`Api → Infrastructure → Application ← Infrastructure`, all → `Domain`)
- Dependency direction not violated (no `Api` → EF direct, no `Domain` → framework deps)
- No parallel implementation of something that already exists unfinished (D-018 pattern)
- Provider boundary respected (`Kurx.Application.Abstractions` interfaces, no direct external SDK calls outside `Infrastructure/Providers`)

## Output

List each structural deviation with file:line, and whether it needs a `D-NNN` or is a straightforward fix.

## Stop condition

Every deviation is either fixed or has a recorded decision explaining why it's intentional.
