// Card and Stat moved to ./content.tsx, where the rest of the card family lives.
// Re-export shim so existing import paths keep working — single source.
export { Card, Stat } from "./content";
