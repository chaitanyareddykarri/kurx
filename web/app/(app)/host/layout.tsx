/// Host-surface chrome. Deliberately bare: the workspace nav lives in **/workspace**, which lists the
/// caller's own events (D-267). This used to resolve a "current organization" from a cookie, render an
/// organization switcher, and gate every child page behind having one — the org-first shell that made
/// event management something you reached *through* an organization. Pages under here are event-scoped
/// and resolve their organization from the event itself.
export default function HostLayout({ children }: { children: React.ReactNode }) {
  return <div className="space-y-6">{children}</div>;
}
