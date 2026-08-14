import { ChipRail } from "@/components/ui/chip-rail";
import type { Kind } from "@/lib/api";

/** Kind quick-browse rail — mirrors Flutter's `KindChips`, both thin wrappers over their platform's
 * shared chip primitive (`ChipRail` here, `KurxChip` there) driving Phase 16's Kind-based discovery. */
export function KindChips({
  kinds,
  activeSlug,
  hrefFor,
  ariaLabel
}: {
  kinds: Kind[];
  activeSlug?: string;
  hrefFor: (slug: string | undefined) => string;
  ariaLabel: string;
}) {
  if (kinds.length === 0) return null;
  return (
    <ChipRail
      ariaLabel={ariaLabel}
      items={kinds.map((k) => ({
        key: k.slug,
        label: k.name,
        // Clicking the active kind clears it, exactly as before.
        href: hrefFor(activeSlug === k.slug ? undefined : k.slug),
        selected: activeSlug === k.slug
      }))}
    />
  );
}
