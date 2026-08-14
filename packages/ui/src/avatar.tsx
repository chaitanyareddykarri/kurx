function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0][0]!.toUpperCase();
  return (parts[0][0]! + parts[parts.length - 1][0]!).toUpperCase();
}

/** Circular avatar with a deterministic initials fallback — seeded users never render a broken image. */
export function Avatar({ name, src, size = 40 }: { name: string; src?: string; size?: number }) {
  if (src) {
    // eslint-disable-next-line @next/next/no-img-element
    return <img src={src} alt={name} width={size} height={size} className="rounded-full object-cover" />;
  }
  const hue = (name.split("").reduce((a, ch) => a + ch.charCodeAt(0), 0) * 37) % 360;
  return (
    <span
      style={{ width: size, height: size, backgroundColor: `hsl(${hue} 50% 50% / 0.2)`, fontSize: size * 0.36 }}
      className="inline-grid shrink-0 place-items-center rounded-full border border-border font-bold text-text"
      aria-hidden
    >
      {initials(name)}
    </span>
  );
}
