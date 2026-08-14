# Checklist: Release

- [ ] CI (`.github/workflows/ci.yml`) green on the branch
- [ ] `docker compose up --build` boots postgres/redis/backend/web/admin all healthy
- [ ] `/health` reflects real dependency probes, not a static 200
- [ ] Phase completion criteria (`.claude/phases/phaseN.md`) actually met
- [ ] `docs/roadmap/README.md` status updated
- [ ] Live smoke test of the core affected flow(s) passed
- [ ] No unreviewed prod secret/infra change bundled in
