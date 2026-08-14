// Moved to the shared design system (@kurx/ui). Re-export shim — single source.
//
// This file used to hold its own hardcoded hex copy of the palette, which made it
// the third of four uncoordinated copies (alongside tokens.css, the Tailwind
// preset, and web's inlined tailwind.config.ts). Nothing checked them against
// each other, so a token edit could silently apply to admin and not to web
// (D-285). The values now come from `@kurx/ui`, which is asserted against
// tokens.css by packages/ui/tokens.test.ts.
export { themeHex, themeHexLight } from "@kurx/ui";
