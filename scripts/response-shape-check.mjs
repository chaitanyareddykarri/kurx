#!/usr/bin/env node
// Proves a declared response type is the type the endpoint actually returns (D-313).
//
// `openapi-response-check.mjs` proves an operation *declares* something. It cannot prove the declaration
// is TRUE. `.Produces<T>()` is metadata the C# compiler never validates against the handler body, so a
// wrong T compiles, ships, and is strictly worse than no declaration at all: clients are generated from
// it and `contract-check.mjs` then happily validates them against a lie.
//
// This closes that gap for the case that carries the risk — an endpoint whose body returns a
// hand-written `To*Json(...)` mapper. Those mappers now have declared return types, so the mapper's
// signature is ground truth; the check is simply that `.Produces<T>()` names the same type.
//
// Not covered (and honestly so): endpoints returning an inline anonymous object, which have no type to
// compare against. Those are exactly the ones still on the undeclared allow-list.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const DIR = path.join(ROOT, "backend/Kurx.Api/Endpoints");
const VERBS = ["MapGet", "MapPost", "MapPut", "MapPatch", "MapDelete"];

let checked = 0, mismatches = 0, unverifiable = 0;

for (const file of fs.readdirSync(DIR).filter((f) => f.endsWith(".cs"))) {
  const src = fs.readFileSync(path.join(DIR, file), "utf8");

  // mapper name -> declared return type (only mappers that return a named type are ground truth).
  // A name declared more than once is an OVERLOAD SET resolved by argument type, which this textual
  // check cannot disambiguate — CategoryEndpoints carries six `ToJson`. Those are dropped and counted
  // as unverifiable rather than guessed at; a checker that reports a confident wrong answer gets
  // switched off, which is the failure mode this whole gate exists to avoid.
  const seen = new Map(), overloaded = new Set();
  for (const m of src.matchAll(/private static ([A-Za-z0-9_<>,? ]+?) (To[A-Za-z]*Json)\s*\(/g)) {
    const type = m[1].trim();
    if (type === "object" || type === "IResult") continue;   // still anonymous / not a shape
    if (seen.has(m[2])) overloaded.add(m[2]);
    seen.set(m[2], type);
  }
  const mappers = new Map([...seen].filter(([fn]) => !overloaded.has(fn)));
  unverifiable += overloaded.size;
  if (!mappers.size) continue;

  for (const verb of VERBS) {
    for (const m of src.matchAll(new RegExp(`\\w+\\.${verb}\\(`, "g"))) {
      const start = m.index + m[0].length;
      let d = 1, i = start, s = false, lc = false, bc = false;
      while (i < src.length && d > 0) {
        const c = src[i], n = src[i + 1];
        if (lc) { if (c === "\n") lc = false; i++; continue; }
        if (bc) { if (c === "*" && n === "/") { bc = false; i++; } i++; continue; }
        if (s) { if (c === "\\") { i += 2; continue; } if (c === '"') s = false; i++; continue; }
        if (c === "/" && n === "/") { lc = true; i += 2; continue; }
        if (c === "/" && n === "*") { bc = true; i += 2; continue; }
        if (c === '"') { s = true; i++; continue; }
        if (c === "(") d++; else if (c === ")") d--;
        i++;
      }
      const body = src.slice(start, i - 1);

      let j = i, sd = 0, ss = false, term = -1;
      while (j < src.length) {
        const c = src[j];
        if (ss) { if (c === "\\") { j += 2; continue; } if (c === '"') ss = false; j++; continue; }
        if (c === '"') { ss = true; j++; continue; }
        if ("([{".includes(c)) sd++; else if (")]}".includes(c)) sd--;
        else if (c === ";" && sd === 0) { term = j; break; }
        j++;
      }
      if (term === -1) continue;
      const declared = src.slice(i, term).match(/\.Produces<([^;]+?)>\(\)/)?.[1];
      if (!declared) continue;

      // The endpoint constructs the declared type directly (e.g. a *Page wrapping a mapped list).
      // The compiler already proves that, and the mapper inside it is a constructor argument, not the
      // response shape — comparing against it would be wrong.
      const declaredBare = declared.replace(/^(?:IReadOnlyList|IEnumerable|List|ICollection)<(.+)>$/, "$1");
      if (new RegExp(`Results\\.Ok\\(\\s*new ${declaredBare}\\(`).test(body)) continue;

      // which mapper does this endpoint's success path use, and is it scalar or a list?
      let used = null, isList = false;
      for (const [fn, type] of mappers) {
        if (new RegExp(`\\.Select\\(${fn}\\)`).test(body)) { used = type; isList = true; break; }
        if (new RegExp(`Results\\.Ok\\(\\s*(await\\s+)?${fn}\\(`).test(body)) { used = type; break; }
      }
      if (!used) continue;

      checked++;
      const route = body.match(/^\s*"([^"]*)"/)?.[1] ?? "(inline)";
      const where = `${file}  ${verb.replace("Map", "").toUpperCase()} ${route}`;

      const inner = declared.match(/^(?:IReadOnlyList|IEnumerable|List|ICollection)<(.+)>$/)?.[1];
      const declaredIsList = inner !== undefined;
      const declaredElem = inner ?? declared;

      if (declaredElem !== used) {
        console.error(`::error::${where} declares ${declared} but returns ${isList ? `a list of ${used}` : used}.`);
        mismatches++;
      } else if (declaredIsList !== isList) {
        console.error(
          `::error::${where} declares ${declared} but the body returns `
          + `${isList ? "a LIST" : "a SINGLE object"} — list-ness disagrees.`);
        mismatches++;
      }
    }
  }
}

console.log(
  `response-shape-check: ${checked} mapper-backed declaration(s) verified against the mapper's own `
  + `return type — ${mismatches} mismatch(es), ${unverifiable} overloaded mapper name(s) skipped.`);
if (mismatches) {
  console.error("A wrong .Produces<T>() is worse than none: clients are generated from it.");
  process.exit(1);
}
