#!/usr/bin/env node
// Contract validation: OpenAPI ⇄ Web ⇄ Admin ⇄ Flutter.
//
// WHY THIS EXISTS
// ---------------
// The committed spec proves *backend ↔ spec*. Nothing proved *spec ↔ client*, and all three clients
// hand-maintain their own request/response models. That gap has shipped the same bug three times:
//
//   D-245  web + admin declared snake_case for a wallet response that serializes camelCase, so
//          `.parse()` threw on every call and two finance pages rendered blank.
//   D-292  web/lib/chat-api.ts declared camelCase against a snake_case API, so every parse threw,
//          `/chats` caught it and rendered "No event chats" for everyone.
//   #3     meSchema declared `can_organize_paid` FLAT while the wire nests it under `trust`, with
//          `.optional().default(false)` — so the key was never present, zod filled the default, and
//          paid hosting read as unavailable to every user on every request.
//
// Every one of those is the same shape: **a client declares a field the wire does not have, and a
// default or a catch makes the mismatch silent**. This script makes that a failed build.
//
// WHAT IT CHECKS
// --------------
//   unknown_field      client declares a key absent from the spec schema  → the D-245/D-292/#3 bug
//   missing_required   spec marks a field required, client never declares it → data silently dropped
//   optionality        spec requires it, client marks it optional/defaulted → the mask that hid #3
//
// WHY NOT GENERATE CLIENTS
// ------------------------
// Generation would replace 2,200 lines of hand-tuned web models, every admin model and every Flutter
// DTO — a migration with its own bug surface, to solve a problem that is fundamentally *verification*.
// A gate costs a fraction and fails in the same place a generator would have.
//
// WHY AN EXPLICIT MAP
// -------------------
// Nothing links `meSchema` to `MeResponse` by name, and inferring the pairing would produce confident
// nonsense. `docs/api/contract-map.json` states each pairing once. Unmapped models are reported as
// coverage, never as passes — the script never claims to have checked something it did not.

import { readFileSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const SPEC = join(ROOT, "docs/api/openapi.json");
const MAP = join(ROOT, "docs/api/contract-map.json");

// ── Spec ────────────────────────────────────────────────────────────────────

/** `{ [schemaName]: { fields: Set<string>, required: Set<string> } }` */
export function loadSpec() {
  const spec = JSON.parse(readFileSync(SPEC, "utf8"));
  const schemas = spec.components?.schemas ?? {};
  const out = {};
  for (const [name, schema] of Object.entries(schemas)) {
    if (!schema.properties) continue;
    // `nullable` is per-property; a nullable field may be present with a null value, which is a
    // different statement from "may be absent". A client that treats it as non-nullable throws on a
    // perfectly legal response, so the two are tracked separately.
    const nullable = new Set(
      Object.entries(schema.properties)
        .filter(([, v]) => v.nullable === true)
        .map(([k]) => k)
    );
    out[name] = {
      fields: new Set(Object.keys(schema.properties)),
      // OpenAPI omits `required` entirely when nothing is required — absent means "all optional",
      // not "unknown", so an empty set is the correct reading rather than a reason to skip.
      required: new Set(schema.required ?? []),
      nullable,
    };
  }
  return out;
}

// ── Shared: brace-aware top-level key extraction ────────────────────────────

/**
 * Returns the substring inside the balanced brackets that open at `openIndex`.
 * Brace counting rather than a regex is what keeps a nested `z.object({...})` or a nested record
 * from leaking its keys into the parent — the whole point of the check is nesting.
 */
function balanced(src, openIndex, open = "{", close = "}") {
  let depth = 0;
  for (let i = openIndex; i < src.length; i++) {
    if (src[i] === open) depth++;
    else if (src[i] === close) {
      depth--;
      if (depth === 0) return src.slice(openIndex + 1, i);
    }
  }
  return "";
}

/** Splits a body into top-level `key: value` entries, ignoring anything nested. */
function topLevelEntries(body) {
  const entries = [];
  let depth = 0;
  let start = 0;
  for (let i = 0; i < body.length; i++) {
    const c = body[i];
    if (c === "{" || c === "(" || c === "[") depth++;
    else if (c === "}" || c === ")" || c === "]") depth--;
    else if (c === "," && depth === 0) {
      entries.push(body.slice(start, i));
      start = i + 1;
    }
  }
  entries.push(body.slice(start));
  return entries.map((e) => e.trim()).filter(Boolean);
}

// ── Web / Admin: zod ────────────────────────────────────────────────────────

/**
 * Extracts `export const <name> = z.object({ ... })` into `{ [name]: {field: {optional}} }`.
 *
 * Comment lines are stripped first: these files are heavily commented and a `//` line mentioning a
 * field name would otherwise register as a declaration.
 */
export function loadZod(files) {
  const models = {};
  for (const rel of files) {
    const path = join(ROOT, rel);
    if (!existsSync(path)) continue;
    const src = readFileSync(path, "utf8")
      .replace(/^\s*\/\/.*$/gm, "")
      .replace(/\/\*[\s\S]*?\*\//g, "");

    const re = /export const (\w+)\s*=\s*z\.object\(/g;
    let m;
    while ((m = re.exec(src)) !== null) {
      const braceAt = src.indexOf("{", m.index + m[0].length - 1);
      if (braceAt === -1) continue;
      const body = balanced(src, braceAt);
      const fields = {};
      for (const entry of topLevelEntries(body)) {
        const km = entry.match(/^["']?([A-Za-z0-9_]+)["']?\s*:/);
        if (!km) continue;
        const value = entry.slice(km[0].length);
        fields[km[1]] = {
          // `.default()` counts as optional: it is precisely what let a missing key look present.
          optional: /\.optional\(\)|\.default\(/.test(value),
          // `.nullable()` / `.nullish()` accept an explicit null. Distinct from optional: zod throws
          // on null for a merely-optional field, which is how a nullable server field crashes a
          // client that only marked it optional.
          nullable: /\.nullable\(\)|\.nullish\(\)/.test(value),
        };
      }
      if (Object.keys(fields).length > 0) models[m[1]] = { fields, source: rel };
    }
  }
  return models;
}

// ── Flutter: freezed factory constructors ───────────────────────────────────

/**
 * Extracts `const factory X({ ... })` from a freezed class into `{ [X]: {wireKey: {optional}} }`.
 *
 * Generated `*.freezed.dart` / `*.g.dart` are skipped — they mirror the hand-written declaration, so
 * including them would double-count and, worse, make a hand-written mistake look confirmed.
 */
export function loadFlutter(files) {
  const models = {};
  for (const rel of files) {
    const path = join(ROOT, rel);
    if (!existsSync(path) || /\.(freezed|g)\.dart$/.test(rel)) continue;
    const src = readFileSync(path, "utf8").replace(/^\s*\/\/.*$/gm, "");

    const re = /const factory (\w+)\(\{/g;
    let m;
    while ((m = re.exec(src)) !== null) {
      const braceAt = src.indexOf("{", m.index + m[0].length - 2);
      const body = balanced(src, braceAt);
      const fields = {};
      for (const entry of topLevelEntries(body)) {
        // `@JsonKey(name: 'x')` wins; otherwise json_serializable derives the wire key from the Dart
        // field name, which this project writes in camelCase → snake_case.
        const keyed = entry.match(/@JsonKey\(\s*name:\s*['"]([A-Za-z0-9_]+)['"]/);
        const decl = entry.match(/(\w+)\s*,?\s*$/);
        if (!decl) continue;
        const wire = keyed ? keyed[1] : decl[1].replace(/[A-Z]/g, (c) => "_" + c.toLowerCase());
        const isNullable = /\?\s+\w+\s*,?\s*$/.test(entry);
        fields[wire] = {
          // A nullable type or a @Default both mean "absent is survivable" — the same mask.
          optional: /@Default\(/.test(entry) || isNullable,
          nullable: isNullable,
        };
      }
      if (Object.keys(fields).length > 0) models[m[1]] = { fields, source: rel };
    }
  }
  return models;
}

// ── Key transforms ──────────────────────────────────────────────────────────

/**
 * Some clients deliberately normalise wire keys before parsing, so their model field names are NOT
 * the wire keys. `web/lib/chat-api.ts` is the case in hand: D-292 chose to run `camelizeKeys()` on
 * the way in and keep camelCase schemas, because those names are that module's public surface across
 * fifteen files. Comparing its models to snake_case spec keys reports eighteen violations against
 * entirely correct code.
 *
 * That matters more than it looks. **A gate that fails on correct code gets switched off**, and then
 * it protects nothing — so the checker has to model the adapter rather than pretend it is absent. The
 * transform is declared per model in the map, which also documents that the adapter exists.
 */
const TRANSFORMS = {
  identity: (k) => k,
  // snake_case → camelCase, matching chat-api.ts's camelizeKeys exactly.
  snake_to_camel: (k) => k.replace(/_([a-z0-9])/g, (_, c) => c.toUpperCase()),
};

// ── Comparison ──────────────────────────────────────────────────────────────

export function compare(specSchema, specName, clientName, client, surface, findings, transformName) {
  const transform = TRANSFORMS[transformName ?? "identity"];
  if (!transform) {
    findings.push({
      level: "error",
      surface,
      message: `contract-map sets transform "${transformName}" for ${specName}, which contract-check does not implement.`,
      source: "docs/api/contract-map.json",
    });
    return;
  }
  // The spec keys as the client will actually see them, after its own normalisation.
  const specFields = new Set([...specSchema.fields].map(transform));
  const specRequired = new Set([...specSchema.required].map(transform));

  const specNullable = new Set([...specSchema.nullable].map(transform));

  for (const [field, meta] of Object.entries(client.fields)) {
    // (1) UNKNOWN / RENAMED / RE-NESTED. A rename leaves the old name behind; moving a field into a
    // sub-object leaves the old top-level name behind. Both surface here, which is why this one
    // check catches D-245, D-292 and the can_organize_paid bug alike.
    if (!specFields.has(field)) {
      findings.push({
        level: "error",
        surface,
        message:
          `${clientName} declares "${field}", which ${specName} does not have — renamed, removed, or ` +
          `moved into a nested object. A key the wire never sends parses as undefined, and with a ` +
          `default it does so silently.`,
        source: client.source,
      });
      continue;
    }

    // (2) NULLABLE MISMATCH — an error, because it breaks on a *legal* response. The server may send
    // an explicit null; a client that did not allow one throws at parse time.
    if (specNullable.has(field) && !meta.nullable && !meta.optional) {
      findings.push({
        level: "error",
        surface,
        message:
          `${clientName}.${field} does not accept null, but ${specName} declares it nullable. ` +
          `The server may legitimately send null, and this client throws when it does.`,
        source: client.source,
      });
    }

    // (3) OPTIONALITY — a WARNING, deliberately. A client marking a server-required field optional is
    // usually defending against an older backend, which is legitimate. It is still the mask that hid
    // can_organize_paid, so it is surfaced — but failing the build on defensive coding would train
    // people to disable the gate, and a disabled gate protects nothing.
    if (specRequired.has(field) && meta.optional) {
      findings.push({
        level: "warning",
        surface,
        message:
          `${clientName}.${field} is optional/defaulted while ${specName} marks it required. ` +
          `Legitimate as backward compatibility, but it is also what lets a rename pass unnoticed.`,
        source: client.source,
      });
    }
  }

  // (4) MISSING REQUIRED — a warning: the client simply does not consume the field. Worth seeing
  // (zod strips undeclared keys, so the data is dropped) but not a build failure on its own.
  for (const field of specRequired) {
    if (!(field in client.fields)) {
      findings.push({
        level: "warning",
        surface,
        message: `${clientName} never declares "${field}", which ${specName} marks required — the value is dropped on parse.`,
        source: client.source,
      });
    }
  }
}

// ── Main ────────────────────────────────────────────────────────────────────

function main() {
  if (!existsSync(SPEC)) {
    console.error(`::error::${SPEC} is missing. Generate it with scripts/generate-openapi.sh.`);
    process.exit(1);
  }
  if (!existsSync(MAP)) {
    console.error(`::error::${MAP} is missing. It states which client model implements which schema.`);
    process.exit(1);
  }

  const spec = loadSpec();
  const config = JSON.parse(readFileSync(MAP, "utf8"));
  const web = loadZod(config.sources.web);
  const admin = loadZod(config.sources.admin);
  const flutter = loadFlutter(config.sources.flutter);

  const findings = [];
  let checked = 0;

  for (const entry of config.models) {
    const specSchema = spec[entry.spec];
    if (!specSchema) {
      findings.push({
        level: "error",
        surface: "spec",
        message: `contract-map names schema "${entry.spec}", which the spec does not define. It was renamed or removed.`,
        source: "docs/api/contract-map.json",
      });
      continue;
    }
    for (const [surface, models] of [["web", web], ["admin", admin], ["flutter", flutter]]) {
      const name = entry[surface];
      if (!name) continue;
      const client = models[name];
      if (!client) {
        findings.push({
          level: "error",
          surface,
          message: `contract-map names ${surface} model "${name}", which was not found in the configured sources.`,
          source: "docs/api/contract-map.json",
        });
        continue;
      }
      compare(specSchema, entry.spec, name, client, surface, findings, entry.transform);
      checked++;
    }
  }

  const errors = findings.filter((f) => f.level === "error");
  for (const f of findings) {
    const prefix = f.level === "error" ? "::error::" : "::warning::";
    console.log(`${prefix}[${f.surface}] ${f.message}  (${f.source})`);
  }

  const warnings = findings.filter((f) => f.level === "warning");
  console.log(
    `\ncontract-check: ${checked} client model(s) validated against ${Object.keys(spec).length} spec schemas ` +
      `— ${errors.length} error(s), ${warnings.length} warning(s).`
  );

  if (errors.length > 0) {
    console.error(`\n::error::${errors.length} contract violation(s). Fix the client model, or regenerate the spec if the API changed deliberately.`);
    process.exit(1);
  }
  console.log("No contract drift.");
}

// Only when executed as a script. `contract-check.test.mjs` imports the comparison logic to prove each
// finding category actually fires, and an unconditional call here would run the whole real check —
// against the real spec — as a side effect of that import.
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) main();
