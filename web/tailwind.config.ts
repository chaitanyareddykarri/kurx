import type { Config } from "tailwindcss";

// Web consumes the shared Kurx Tailwind preset — the single source of truth for
// design tokens. This file previously inlined a verbatim copy of the preset's
// theme block while admin correctly consumed the preset, so the two surfaces
// could drift apart on any token edit (D-285). Only `content` is web's own.
//
// `require` rather than an ESM import: the preset is CommonJS, and Tailwind's
// config loader resolves it without TS/ESM interop friction — the same reason
// admin/tailwind.config.js is CJS.
// eslint-disable-next-line @typescript-eslint/no-var-requires
const kurxPreset = require("../packages/ui/tailwind-preset.cjs");

const config: Config = {
  presets: [kurxPreset],
  content: [
    "./app/**/*.{ts,tsx}",
    "./components/**/*.{ts,tsx}",
    "./lib/**/*.{ts,tsx}",
    // Shared primitives live here — include so their classes survive purge.
    "../packages/ui/src/**/*.{ts,tsx}"
  ]
};

export default config;
