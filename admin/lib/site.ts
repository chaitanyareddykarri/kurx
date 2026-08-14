// The browser reaches the API via the public host URL (NEXT_PUBLIC_API_URL, baked at build);
// server-side renders run inside the container and use the internal API_URL, read at runtime.
// The branch is required: API_URL is a private var, so it does not survive into the client
// bundle, and `??` on the substituted value would pin apiBaseUrl to the wrong origin there.
// Same resolution as web/lib/site.ts.
export const siteConfig = {
  name: "Kurx Admin",
  apiBaseUrl:
    typeof window === "undefined"
      ? process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080"
      : process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080",
  // The public site, which is a DIFFERENT ORIGIN from the console. Two links opened the public
  // event and organisation pages with a relative href, so they resolved against the admin origin
  // and 404'd — the routes exist, in `web/app/e/[slug]` and `web/app/o/[slug]`, on the other
  // surface. A relative link is only ever right within one app.
  webBaseUrl: process.env.NEXT_PUBLIC_WEB_URL ?? "http://localhost:3000"
} as const;

/// The PUBLIC site's origin — the mirror of web's `NEXT_PUBLIC_ADMIN_URL` (D-195).
///
/// Every `/e/{slug}`, `/u/{username}` and `/o/{slug}` page is served by web, not by this console. Linking
/// to one relatively resolves against the console's own origin and 404s, which in the review workspace
/// means a reviewer is asked to judge an event they cannot open. Absolute, or the link is a dead end.
export const WEB_URL = process.env.NEXT_PUBLIC_WEB_URL ?? "http://localhost:3000";
