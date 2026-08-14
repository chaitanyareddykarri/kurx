// Generates docs/api/UNDECLARED_TRACKER.md from the spec + allow-list + endpoint sources, so the
// tracker can never drift from reality. Run after any regeneration.
import fs from "node:fs";
import path from "node:path";

const spec = JSON.parse(fs.readFileSync("docs/api/openapi.json", "utf8"));
const list = JSON.parse(fs.readFileSync("docs/api/undeclared-allowlist.json", "utf8"));
const METHODS = ["get", "post", "put", "patch", "delete"];

// Re-derive the intentional set from the check script so there is exactly one source of truth.
const checkSrc = fs.readFileSync("scripts/openapi-response-check.mjs", "utf8");
const intentional = [...checkSrc.matchAll(/^  "([A-Z]+ \/[^"]+)":$/gm)].map((m) => m[1]);

let total = 0;
const undeclared = [];
for (const [route, item] of Object.entries(spec.paths)) {
  for (const [m, op] of Object.entries(item)) {
    if (!METHODS.includes(m)) continue;
    total++;
    const r = op.responses ?? {};
    if (r["204"]) continue;
    const c = r["200"]?.content;
    if (c && Object.keys(c).length) continue;
    undeclared.push({ op: `${m.toUpperCase()} ${route}`, tag: (op.tags || [])[0] || "—" });
  }
}
undeclared.sort((a, b) => a.op.localeCompare(b.op));

// group pending by surface
const surface = (o) => {
  const p = o.split(" ")[1].split("/").filter(Boolean);
  if (p[0] !== "v1") return "well-known";
  if (["admin"].includes(p[1])) return "admin";
  if (["auth"].includes(p[1])) return "auth";
  if (["me"].includes(p[1])) return "me";
  return p[1] ?? "other";
};
const pending = undeclared.filter((u) => !intentional.includes(u.op));
const groups = {};
for (const u of pending) (groups[surface(u.op)] ??= []).push(u);

const rows = Object.entries(groups).sort((a, b) => b[1].length - a[1].length);

let md = `# Undeclared response tracker

<!-- GENERATED — do not edit by hand.
     Regenerate: node scripts/openapi-response-check.mjs --update && node scripts/tracker.mjs -->

Every operation in \`docs/api/openapi.json\` whose success response carries no schema, tracked to
closure. Generated from the spec, the allow-list and \`scripts/openapi-response-check.mjs\`, so it
cannot drift from what CI actually enforces ([D-313](../DECISIONS.md)).

**An undeclared response is not merely undocumented — it is invisible to \`contract-check.mjs\`**, which
then has nothing to validate the three hand-written client model sets against and passes vacuously. That
is the D-303 failure mode, and it is why this list exists rather than a "someday" note.

## Status

| | Count |
|---|---:|
| Total operations | **${total}** |
| Declared | **${total - undeclared.length}** |
| Undeclared — intentional | **${intentional.length}** |
| Undeclared — pending | **${pending.length}** |

The ratchet (\`scripts/openapi-response-check.mjs\`, CI-enforced) fails on any *new* undeclared response
and on a *stale* allow-list entry, so **pending can only decrease**.

## Intentional — never to be "fixed"

Each is a decision with a reason recorded in \`scripts/openapi-response-check.mjs\`. Declaring any of
them would misdescribe the wire.

| Operation | Why |
|---|---|
`;

const WHY = {
  "POST /v1/gate/{eventId}/scan":
    "**Polymorphic 200** — a duplicate scan and a successful scan return different shapes. OpenAPI 3.0 allows one schema per status code, so any declaration is wrong half the time. A union would emit nulls the wire never sends; 409 would change status semantics for every scanner.",
  "POST /v1/webhooks/razorpay": "Empty 200 ack — Razorpay reads only the status; there is no body.",
  "POST /v1/webhooks/whatsapp": "Empty 200 ack — Meta treats any 2xx as delivered.",
  "PUT /v1/storage/{key}": "Local-disk presigned receiver; mapped only when `STORAGE_PROVIDER=localdisk`, so it does not exist in any real deployment (D-110).",
};
for (const o of intentional) md += `| \`${o}\` | ${WHY[o] ?? "See `scripts/openapi-response-check.mjs`."} |\n`;

md += `\n## Pending — ${pending.length} operations\n\nThe live list is \`docs/api/undeclared-allowlist.json\`; this is its readable form.\n`;
md += `\nMost remaining shapes are **singletons** — one small record each, no shared pattern left to exploit.\nWhen closing one, follow the ladder in [\`.claude/memory/api-conventions.md\`](../../.claude/memory/api-conventions.md):\nreuse an existing contract first, and **declare the record in \`Kurx.Application.Abstractions\`** or it\nsilently serializes camelCase.\n`;

for (const [g, ops] of rows) {
  md += `\n### ${g} — ${ops.length}\n\n`;
  for (const o of ops) md += `- \`${o.op}\`\n`;
}

md += `\n## How to close one\n
1. Read the endpoint and determine the **actual** runtime shape — do not infer it from the route name.
2. Search for an existing contract to reuse. Many views are already exact 1:1 projections.
3. Only then add the smallest record, in \`Kurx.Application.Abstractions\`.
4. Add \`.Produces<T>()\`.
5. Regenerate the spec and **diff it** — \`.Produces<T>()\` is metadata the C# compiler never checks, so
   this is the only step that catches a wrong declaration.
6. \`node scripts/openapi-response-check.mjs --update\` to drop it from the list, then regenerate this file.

**Two traps that cost real time:** a mapper that *translates* (57 \`.ToLowerInvariant()\` calls across the
API turn \`"Published"\` into the \`"published"\` the wire carries) must keep its translation; and an
envelope (\`{items, total}\`) must never be declared as its element type.
`;

fs.writeFileSync("docs/api/UNDECLARED_TRACKER.md", md);
console.log(`wrote docs/api/UNDECLARED_TRACKER.md — ${total} ops, ${intentional.length} intentional, ${pending.length} pending`);
