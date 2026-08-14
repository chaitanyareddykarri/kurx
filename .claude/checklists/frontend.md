# Checklist: Frontend

- [ ] Server Components/Actions used unless interactivity requires `"use client"`
- [ ] Session via httpOnly cookies only, no token exposed to client JS
- [ ] Env var names for API base URL match actual runtime config, no stale fallback
- [ ] Typecheck + lint + build green
- [ ] Page loaded in a real browser against a real session (not assumed from types)
- [ ] `admin/` is a real, built-out console (`admin/STATUS.md`), not a scaffold — no unrequested changes outside what was asked
