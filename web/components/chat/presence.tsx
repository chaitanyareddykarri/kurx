"use client";

import { Avatar } from "@/components/ui/avatar";
import { typingLabel, type TypingUser } from "@/lib/chat-presence";

/// An avatar with an online dot (D-114).
///
/// The dot is rendered only when presence is actually known. With no shared presence store on the
/// server the indicator is absent entirely rather than grey — "we cannot know" and "nobody is here"
/// must not look the same.
export function PresenceAvatar({
  name,
  size = 32,
  online,
  presenceKnown
}: {
  name: string;
  size?: number;
  online: boolean;
  presenceKnown: boolean;
}) {
  if (!presenceKnown) return <Avatar name={name} size={size} />;

  const dot = Math.round(size * 0.3);
  return (
    // Announced as words, never colour alone.
    <span className="relative inline-block" role="img" aria-label={`${name}, ${online ? "online" : "offline"}`}>
      <Avatar name={name} size={size} />
      <span
        aria-hidden
        style={{ width: dot, height: dot }}
        // Ringed in the page background so the dot reads against any avatar tint, which is what
        // keeps contrast acceptable in both themes.
        className={[
          "absolute bottom-0 right-0 rounded-full ring-2 ring-bg",
          online ? "bg-success" : "bg-muted"
        ].join(" ")}
      />
    </span>
  );
}

/// "Alice is typing…" above the composer.
///
/// Takes the already-derived list rather than the whole presence state, so it re-renders on typing
/// changes alone and never on a roster or receipt update.
export function TypingBanner({ typists }: { typists: readonly TypingUser[] }) {
  const label = typingLabel(typists);

  return (
    // A live region that is always mounted: announcing "X is typing" is useful, but mounting and
    // unmounting the region itself makes some screen readers miss the first message.
    <p
      aria-live="polite"
      aria-atomic="true"
      className="flex h-5 items-center gap-2 px-5 text-xs italic text-muted"
    >
      {label && (
        <>
          <TypingDots />
          <span className="truncate">{label}</span>
        </>
      )}
    </p>
  );
}

/// Three dots pulsing in sequence. Hidden from assistive tech — the banner already says it in words,
/// and the animation has nothing to add.
function TypingDots() {
  return (
    <span aria-hidden className="flex shrink-0 items-center gap-0.5">
      {[0, 1, 2].map((i) => (
        <span
          key={i}
          // Staggered by a third of the cycle each. `motion-reduce` stops the animation for users who
          // have asked the OS for less motion; the label still conveys the state.
          style={{ animationDelay: `${i * 160}ms` }}
          className="h-1 w-1 animate-pulse rounded-full bg-muted motion-reduce:animate-none"
        />
      ))}
    </span>
  );
}

/// How many other people are in the room, or nothing at all when presence is unavailable.
export function OnlineCount({ count }: { count: number | undefined }) {
  if (count === undefined || count === 0) return null;
  return (
    <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-success/30 px-2.5 py-1 text-xs text-success">
      <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-success" />
      {count === 1 ? "1 other online" : `${count} others online`}
    </span>
  );
}
