# Kurx Web

> **Architecture: event-first ([D-074](../docs/DECISIONS.md)).** No organizer/owner accounts; event creation drives org representation. See [`../docs/architecture/event-creation.md`](../docs/architecture/event-creation.md). The legacy org-first host UI (`/host/organizations/*`, org "Owner") reflects D-055, pending migration.

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
