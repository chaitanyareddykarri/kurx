/**
 * A `returnTo` arrives in a URL query parameter, so it is attacker-supplied: only a same-app path under
 * `/host/` is ever followed.
 *
 * The test is deliberately not the usual `startsWith("/")` — that admits a protocol-relative
 * `//evil.example`, which a browser treats as an absolute URL to another origin. An array is rejected
 * outright: a repeated `?returnTo=a&returnTo=b` reaches Next as one, and `.startsWith` on it throws at
 * request time, which is a 500 on a URL anyone can type.
 *
 * A leaf module with no imports, because both a server component and a client component need it: the
 * redirect after a representation request is performed in the BROWSER now (the upload before it has to
 * be), so the guard has to live where a client component can reach it.
 */
export function safeReturnTo(value: string | string[] | undefined): string | undefined {
  const candidate = Array.isArray(value) ? value[0] : value;
  return candidate?.startsWith("/host/") ? candidate : undefined;
}
