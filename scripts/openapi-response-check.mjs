#!/usr/bin/env node
// Response-contract ratchet (D-313). Fails when an operation's success response is UNDECLARED and is not
// on the allow-list — i.e. when a new endpoint lands without saying what it returns.
//
// Why a ratchet rather than "all must be declared": 112 operations are still undeclared and converting
// them is staged work. A gate that fails today would simply be switched off, which is how the client
// contract gate spent months reporting a vacuous pass (D-303). This fails only on *regression*, so the
// number can never go back up, and it also fails when the allow-list goes STALE — otherwise the list
// silently becomes permission to never finish.
//
//   node scripts/openapi-response-check.mjs            check
//   node scripts/openapi-response-check.mjs --update   rewrite the allow-list (review the diff!)
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const SPEC = path.join(ROOT, "docs/api/openapi.json");
const LIST = path.join(ROOT, "docs/api/undeclared-allowlist.json");
const METHODS = ["get", "post", "put", "patch", "delete"];

/// Operations that are CORRECT with no response body and must never be "fixed" into a DTO. Anything
/// here is a decision, not a backlog item — each carries its reason.
const INTENTIONAL = {
  "POST /v1/webhooks/razorpay":
    "Empty 200 ack. Razorpay reads only the status; there is no body to describe (D-313).",
  "POST /v1/webhooks/whatsapp":
    "Empty 200 ack. Meta treats any 2xx as delivered (D-313).",
  "PUT /v1/storage/{key}":
    "Local-disk presigned receiver; mapped only when STORAGE_PROVIDER=localdisk, so it does not exist "
    + "in any real deployment (D-110).",

  // The one genuinely polymorphic success response on the platform.
  "POST /v1/gate/{eventId}/scan":
    "Returns TWO legitimate shapes under the SAME 200, and OpenAPI 3.0 permits one schema per status "
    + "code, so any single declaration would misdescribe one of them:\n"
    + "    duplicate → { admitted, is_duplicate, first_checked_in_at, first_checked_in_by, message }\n"
    + "    success   → { admitted, is_duplicate, eligibility_flag }\n"
    + "Every way of declaring it changes something real, and all three were rejected deliberately:\n"
    + "  * a nullable union would start emitting first_checked_in_at/message as null on the success "
    + "path and eligibility_flag as null on the duplicate path — fields the wire does not carry today;\n"
    + "  * moving duplicates to 409 would change status-code semantics for every scanner client, and a "
    + "duplicate scan is not a client error — the gate answers it deliberately, with who scanned and "
    + "when, so door staff can resolve it;\n"
    + "  * splitting the route would change the check-in contract itself.\n"
    + "An honest omission beats a schema that is wrong half the time: a wrong declaration is worse than "
    + "none, because clients are generated from it (see scripts/response-shape-check.mjs). Revisit only "
    + "if the runtime contract is deliberately changed under its own D-NNN.",
};

const spec = JSON.parse(fs.readFileSync(SPEC, "utf8"));

const undeclared = [];
let total = 0;
for (const [route, item] of Object.entries(spec.paths)) {
  for (const [method, op] of Object.entries(item)) {
    if (!METHODS.includes(method)) continue;
    total++;
    const responses = op.responses ?? {};
    // A declared 204 is a complete description of "no body".
    if (responses["204"]) continue;
    const content = responses["200"]?.content;
    // Any content type counts as declared — JSON schemas and binary downloads alike.
    if (content && Object.keys(content).length) continue;
    undeclared.push(`${method.toUpperCase()} ${route}`);
  }
}
undeclared.sort();

const intentional = undeclared.filter((o) => o in INTENTIONAL);
const needsWork = undeclared.filter((o) => !(o in INTENTIONAL));

if (process.argv.includes("--update")) {
  fs.writeFileSync(LIST, JSON.stringify({
    $comment:
      "Operations whose success response is not yet described in the OpenAPI contract (D-313). This is a "
      + "RATCHET: entries may be removed as they get declared, and CI fails if any operation not listed "
      + "here is undeclared. Regenerate with: node scripts/openapi-response-check.mjs --update",
    generated: new Date().toISOString().slice(0, 10),
    count: needsWork.length,
    operations: needsWork,
  }, null, 2) + "\n");
  console.log(`wrote ${path.relative(ROOT, LIST)} — ${needsWork.length} operation(s)`);
  process.exit(0);
}

if (!fs.existsSync(LIST)) {
  console.error(`::error::${path.relative(ROOT, LIST)} is missing. Create it once with --update.`);
  process.exit(1);
}

const allowed = new Set(JSON.parse(fs.readFileSync(LIST, "utf8")).operations);
const added = needsWork.filter((o) => !allowed.has(o));
const stale = [...allowed].filter((o) => !needsWork.includes(o)).sort();

for (const o of added) {
  console.error(
    `::error::${o} has no response schema. Declare it with .Produces<T>() — the DTO must live in `
    + `Kurx.Application.Abstractions or it will serialize camelCase. If the endpoint genuinely has no `
    + `body, use Results.NoContent() + .Produces(StatusCodes.Status204NoContent).`);
}
for (const o of stale) {
  console.error(
    `::error::${o} is on the undeclared allow-list but now HAS a schema. Remove it: `
    + `node scripts/openapi-response-check.mjs --update`);
}

console.log(
  `openapi-response-check: ${total - undeclared.length}/${total} operations declared; `
  + `${undeclared.length} undeclared (${intentional.length} intentional, ${needsWork.length} pending) — `
  + `${added.length} new, ${stale.length} stale.`);

if (added.length || stale.length) process.exit(1);
console.log("No new undeclared responses.");
