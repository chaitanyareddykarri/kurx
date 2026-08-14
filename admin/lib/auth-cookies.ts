/**
 * The console's own session cookie names.
 *
 * **Distinct from web's `kurx_access`/`kurx_refresh`, deliberately.** Cookies are scoped by HOST, not
 * by port: web on `localhost:3000` and this console on `localhost:3001` share one jar, so while both
 * used the same names, signing in on either signed you into both. One number is legitimately both a
 * user and a platform admin — that is the identity model — but the door you walk through decides what
 * opens, and it stopped deciding anything.
 *
 * Separate names make that true regardless of deployment topology. On split production domains
 * (`kurx.com` / `admin.kurx.com`) host-only cookies already kept them apart; this stops that from
 * being a property of where the apps happen to be hosted.
 *
 * Declared here rather than in `session.ts` because `middleware.ts` also needs them and runs on the
 * edge runtime, where `server-only` cannot be imported. Two copies of a cookie name is one rename away
 * from a console that writes a session it cannot read.
 */
export const ACCESS_COOKIE = "kurx_admin_access";
export const REFRESH_COOKIE = "kurx_admin_refresh";
