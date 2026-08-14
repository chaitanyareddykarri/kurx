// @kurx/ui — the Kurx shared design system. Single source of truth for UI
// primitives across web and admin. Design tokens live in ./styles/tokens.css and
// the Tailwind preset (../tailwind-preset.cjs).

// Tokens, JS view — for build-time consumers (OG images, manifest, themeColor)
// that render before any stylesheet exists. Kept in lockstep with tokens.css by
// packages/ui/tokens.test.ts.
export {
  themeHex,
  themeHexLight,
  dark,
  light,
  hex,
  cssVar,
  luminance,
  contrast,
  SURFACE_STEPS,
  TEXT_TOKENS
} from "./tokens";
export type { ColorToken, Palette } from "./tokens";

// Base primitives (shared with web)
export { Button, LinkButton, IconButton, Link } from "./button";
// Content components (Phase 10). Card and Stat moved here from ./card.tsx so the
// whole family lives together; ./card.tsx re-exports for existing import paths.
export {
  Card, LinkCard, CardAction, MetaRow, MediaFrame, Stat, Timeline, EventCard, CategoryCard, TicketCard,
  EventPlaceholder
} from "./content";
export type { TimelineEntry, EventCardStatus } from "./content";
export { Badge, HostTierBadge } from "./badge";
export { Spinner } from "./spinner";

// Primitives added in Phase 6.3-6.5 — none of these existed in the design system.
export { Divider, Tooltip, Progress } from "./feedback-primitives";
export { DateTimeField } from "./datetime";
export { Skeleton, ListSkeleton } from "./skeleton";
export { EmptyState } from "./empty-state";
export { ErrorState } from "./error-state";

// Feedback & whole-region states (Phase 8). The audit found 8 loading.tsx for 88
// web routes and exactly one error.tsx — these are what those routes are built from.
export { Alert, Banner, PermissionDeniedState, NotFoundState, OfflineState } from "./states";
export { Dialog } from "./dialog";
export { useOverlay } from "./use-overlay";

// Non-modal overlays + the command palette (Phase 9.3-9.4).
export { Popover, Menu } from "./menu";
export type { MenuItem } from "./menu";
export { CommandPalette } from "./command-palette";
export type { CommandItem } from "./command-palette";

// Data / admin foundation primitives
export { DataTable } from "./data-table";
export type { Column, SortState, SortDir } from "./data-table";
export { SearchBar } from "./search-bar";
export { FilterBar, FilterSelect } from "./filter-bar";
export { FilterChips } from "./filter-chips";
export type { ActiveFilter } from "./filter-chips";
export { Pagination } from "./pagination";
export { ConfirmDialog } from "./confirm-dialog";
export { StatCard } from "./stat-card";

// Phone (Phase 6, D-089) — one E.164 input + parsing strategy for web and admin.
export { PhoneField } from "./phone-field";
export type { PhoneFieldProps } from "./phone-field";
export { toE164Identifier, isValidPhone } from "./phone-utils";

// Promoted from web's local components/ui (D-185) — same primitives, one source.
export { Avatar } from "./avatar";
export { UserCard } from "./user-card";
export type { UserCardProps } from "./user-card";
export { Chip, ChipLink, chipClass } from "./chip";
export { Switch } from "./switch";
export { Tabs, TabPanel } from "./tabs";
export { SectionHeader } from "./section-header";
export { Breadcrumbs } from "./breadcrumbs";
export type { Crumb } from "./breadcrumbs";
export { Sheet } from "./sheet";
export { ToastProvider, useToast } from "./toast";
export { MotionPanel } from "./motion-panel";
export { Field, Input, Textarea, Select, controlClass } from "./field";

// Form composition (Phase 7) — grouping, submit state, error summary, steps.
export { FormGroup, FormErrorSummary, FormActions, FormBusy, FormSteps } from "./form";

// Selection controls (Phase 6.2) — Checkbox, Radio and Slider did not previously
// exist in the design system and were hand-rolled across 8+ call sites.
export { Checkbox, Radio, RadioGroup, Slider } from "./controls";

// Dependency-free charts (D-185) — no charting library exists in this monorepo; a day-count
// trend line and bar strip are the full extent of what's needed here.
export { Sparkline, MiniBars } from "./sparkline";
export { PROBLEM_COPY, problemMessage, looksLikeCode } from "./problem-copy";
