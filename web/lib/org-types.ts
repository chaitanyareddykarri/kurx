/**
 * The organization-type vocabulary the registry accepts.
 *
 * A leaf module on purpose. It used to live in `lib/api.ts`, which builds an axios client and wraps
 * calls in React's `cache` at module scope — so a client component importing this one constant pulled
 * the whole server-side API layer in with it, and any test rendering that component died on
 * `cache is not a function` before reaching its first assertion. A closed list of ten strings has no
 * business dragging a network client behind it.
 *
 * `lib/api.ts` re-exports it, so existing importers are unaffected.
 */
export const organizationTypes = [
  "college", "school", "university", "company", "startup",
  "ngo", "club", "community", "government", "other"
] as const;
