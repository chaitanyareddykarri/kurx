#!/usr/bin/env node
// Proves contract-check actually detects each violation category.
//
// WHY THIS EXISTS
// ---------------
// A validation gate that reports nothing is indistinguishable from a validation gate that checks
// nothing, and the second failure is silent by construction. That is not hypothetical here: the gate
// shipped with a complete set of checks for required/optional/nullable and detected none of them for
// months, because the spec it reads carried `required` on 36 of 266 schemas — the checks ran, found no
// input, and reported a clean pass. Nobody could tell from the output.
//
// So each category gets a case that must FAIL, and one that must PASS. `node scripts/contract-check.test.mjs`.

import assert from "node:assert/strict";
import { compare, loadZod, loadFlutter, loadSpec } from "./contract-check.mjs";

let run = 0;
const results = [];

function check(name, fn) {
  run++;
  try {
    fn();
    results.push(`  ok   ${name}`);
  } catch (e) {
    results.push(`  FAIL ${name}\n       ${e.message}`);
    process.exitCode = 1;
  }
}

/** Runs one comparison and returns its findings. */
function findings(specSchema, clientFields, transform) {
  const out = [];
  compare(
    {
      fields: new Set(specSchema.fields),
      required: new Set(specSchema.required ?? []),
      nullable: new Set(specSchema.nullable ?? []),
    },
    "SpecModel",
    "clientModel",
    { fields: clientFields, source: "test" },
    "web",
    out,
    transform
  );
  return out;
}

const field = (o = {}) => ({ optional: false, nullable: false, ...o });

// ── 1. UNKNOWN FIELD ────────────────────────────────────────────────────────
check("unknown field is an error", () => {
  const f = findings({ fields: ["id", "name"] }, { id: field(), nope: field() });
  assert.equal(f.length, 1);
  assert.equal(f[0].level, "error");
  assert.match(f[0].message, /"nope"/);
});

// ── 2. RENAMED FIELD ────────────────────────────────────────────────────────
// A rename leaves the old name on the client and the new one unclaimed. The old name is what trips.
check("renamed field is an error (client keeps the old name)", () => {
  const f = findings({ fields: ["issued_at"] }, { created_at: field() });
  assert.equal(f.filter((x) => x.level === "error").length, 1);
  assert.match(f[0].message, /"created_at"/);
});

// ── 3. RE-NESTED FIELD ──────────────────────────────────────────────────────
// The can_organize_paid bug: the wire nests it under `trust`, the client declared it flat.
check("re-nested field is an error (flat client key, nested wire)", () => {
  const f = findings({ fields: ["trust"] }, { can_organize_paid: field({ optional: true }) });
  assert.equal(f.filter((x) => x.level === "error").length, 1);
  assert.match(f[0].message, /nested object/);
});

// ── 4. NULLABLE MISMATCH ────────────────────────────────────────────────────
check("nullable mismatch is an error", () => {
  const f = findings(
    { fields: ["bio"], nullable: ["bio"] },
    { bio: field() } // neither nullable nor optional → throws on a legal null
  );
  assert.equal(f.length, 1);
  assert.equal(f[0].level, "error");
  assert.match(f[0].message, /does not accept null/);
});

check("nullable mismatch is NOT raised when the client allows null", () => {
  const f = findings({ fields: ["bio"], nullable: ["bio"] }, { bio: field({ nullable: true }) });
  assert.equal(f.length, 0);
});

// ── 5. OPTIONALITY MISMATCH ─────────────────────────────────────────────────
check("optionality mismatch is a warning", () => {
  const f = findings({ fields: ["id"], required: ["id"] }, { id: field({ optional: true }) });
  assert.equal(f.length, 1);
  assert.equal(f[0].level, "warning");
  assert.match(f[0].message, /optional\/defaulted/);
});

// ── 6. MISSING REQUIRED ─────────────────────────────────────────────────────
check("missing required field is a warning", () => {
  const f = findings({ fields: ["id", "name"], required: ["id", "name"] }, { id: field() });
  assert.equal(f.length, 1);
  assert.equal(f[0].level, "warning");
  assert.match(f[0].message, /never declares "name"/);
});

// ── 7. TRANSFORM ────────────────────────────────────────────────────────────
// The camelizeKeys adapter must be modelled, or the gate fails on correct code and gets switched off.
check("snake_to_camel transform makes a correct camelCase client clean", () => {
  const f = findings({ fields: ["room_id", "last_message_at"] }, { roomId: field(), lastMessageAt: field() }, "snake_to_camel");
  assert.equal(f.length, 0);
});

check("an unimplemented transform name is an error, not a silent pass", () => {
  const f = findings({ fields: ["a"] }, { a: field() }, "no_such_transform");
  assert.equal(f.length, 1);
  assert.equal(f[0].level, "error");
});

// ── 8. NEGATIVE CONTROL ─────────────────────────────────────────────────────
check("a fully matching model produces no findings", () => {
  const f = findings(
    { fields: ["id", "bio"], required: ["id"], nullable: ["bio"] },
    { id: field(), bio: field({ nullable: true }) }
  );
  assert.equal(f.length, 0);
});

// ── 9. VACUITY GUARDS ───────────────────────────────────────────────────────
// Every check above is decision logic. If the *parsers* silently returned nothing, the real run would
// report a clean pass having compared zero fields — the exact failure this file exists to prevent. So
// assert the parsers extract real fields from the real sources.
check("loadZod extracts fields from the real web client", () => {
  const models = loadZod(["web/lib/api.ts"]);
  assert.ok(models.meSchema, "meSchema not found in web/lib/api.ts");
  assert.ok(Object.keys(models.meSchema.fields).length > 5, "meSchema parsed with suspiciously few fields");
});

check("loadFlutter extracts fields from the real mobile DTO", () => {
  const models = loadFlutter(["mobile/lib/features/auth/data/models/current_user_dto.dart"]);
  assert.ok(models.CurrentUserDto, "CurrentUserDto not found");
  assert.ok(Object.keys(models.CurrentUserDto.fields).length > 5, "CurrentUserDto parsed with suspiciously few fields");
});

// The gate is only as good as the metadata it reads. `required` was empty across the whole spec for
// months while every check above passed, so assert the spec actually carries the inputs.
check("the committed spec carries required + nullable metadata to check against", () => {
  const spec = loadSpec();
  const names = Object.keys(spec);
  assert.ok(names.length > 100, `only ${names.length} schemas parsed from the spec`);
  const withRequired = names.filter((n) => spec[n].required.size > 0).length;
  const withNullable = names.filter((n) => spec[n].nullable.size > 0).length;
  assert.ok(
    withRequired > names.length * 0.5,
    `only ${withRequired}/${names.length} schemas declare any required field — the optionality and ` +
      `missing-required checks have almost nothing to read, so they pass vacuously. Regenerate the ` +
      `spec with RequiredFromNonNullableSchemaFilter registered.`
  );
  assert.ok(withNullable > 0, "no schema declares a nullable property — the nullable check is vacuous");
});

console.log(results.join("\n"));
console.log(`\ncontract-check self-test: ${run} case(s), ${process.exitCode ? "FAILED" : "all passed"}.`);
