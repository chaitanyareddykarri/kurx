# Kurx Web

> **Architecture: user-first and event-first ([D-267](../docs/DECISIONS.md)/[D-268](../docs/DECISIONS.md)).** No organizer/owner accounts and no personal organizations. A **User** owns an event; the organization it *represents* is a field chosen inside the creation form, never a container the event lives in. See [`../docs/architecture/event-creation.md`](../docs/architecture/event-creation.md). The org-first host UI this note used to describe (`/host/organizations/*`, org "Owner") **no longer exists** — the org-scoped routes are `/host/representing/*`, and `/host/events/[id]/*` resolves its org from the event itself (`lib/event-org.ts`), not from a switcher cookie.

Next.js 14 App Router application for the Kurx desktop and PWA experience.

## Setup

```bash
cd web
npm install
npm run dev
```

## Environment

```bash
NEXT_PUBLIC_SITE_URL=https://kurx.app
NEXT_PUBLIC_API_BASE_URL=http://localhost:5080
NEXT_PUBLIC_ANDROID_APP_URL=https://play.google.com/store/apps/details?id=app.kurx
NEXT_PUBLIC_IOS_APP_URL=https://apps.apple.com/app/kurx
NEXT_PUBLIC_IOS_APP_ID=0000000000
NEXT_PUBLIC_RAZORPAY_KEY_ID=
```

Branding is rendered by the `KurxLogo` component (`components/brand/logo.tsx`); no image asset is required.
