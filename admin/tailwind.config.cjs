// Admin consumes the shared Kurx Tailwind preset (single source of truth for
// tokens) so it is visually identical to web. CommonJS keeps the preset resolvable
// without TS/ESM config-loader friction.
const kurxPreset = require("../packages/ui/tailwind-preset.cjs");

/** @type {import('tailwindcss').Config} */
module.exports = {
  presets: [kurxPreset],
  content: [
    "./app/**/*.{ts,tsx}",
    "./components/**/*.{ts,tsx}",
    "./lib/**/*.{ts,tsx}",
    // Shared primitives live here — include so their classes survive purge.
    "../packages/ui/src/**/*.{ts,tsx}"
  ]
};
