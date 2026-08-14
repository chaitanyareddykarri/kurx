"use client";

import { FileText } from "lucide-react";
import type { PostMedia } from "@/lib/posts-api";

/// Media attached to a post. Layout follows count, not kind: 1 fills, 2 splits, 3+ becomes a 2-col
/// grid with the remainder counted on the last tile — so a 10-image post stays one screen tall.
export function PostMediaGrid({ media, compact = false }: { media: PostMedia[]; compact?: boolean }) {
  const images = media.filter((m) => m.content_type.startsWith("image/"));
  const videos = media.filter((m) => m.content_type.startsWith("video/"));
  const docs = media.filter((m) => !m.content_type.startsWith("image/") && !m.content_type.startsWith("video/"));

  const shown = images.slice(0, 4);
  const overflow = images.length - shown.length;
  const height = compact ? "h-28" : "h-56";

  return (
    <div className="mt-3 space-y-2">
      {shown.length > 0 ? (
        <div
          className={`grid gap-1 overflow-hidden rounded-md ${shown.length === 1 ? "grid-cols-1" : "grid-cols-2"}`}
        >
          {shown.map((m, i) => (
            <div key={m.id} className="relative">
              {/* eslint-disable-next-line @next/next/no-img-element -- provider-swappable storage host */}
              <img
                src={m.url}
                /* `alt=""` marks an image decorative, so a screen reader skips it entirely — and
                   these images ARE the post. A reader was told a post existed and never that it had
                   pictures. There is no caption or description field on media, so the alt says what
                   is actually known rather than inventing a description of the contents. */
                alt={`Image ${i + 1} of ${images.length} in this post`}
                loading="lazy"
                className={`w-full object-cover ${shown.length === 1 ? (compact ? "h-40" : "h-80") : height}`}
              />
              {i === shown.length - 1 && overflow > 0 ? (
                <span
                  className="absolute inset-0 grid place-items-center bg-black/55 text-lg font-semibold text-white"
                  aria-hidden
                >
                  +{overflow}
                </span>
              ) : null}
            </div>
          ))}
        </div>
      ) : null}

      {videos.map((m) => (
        // eslint-disable-next-line jsx-a11y/media-has-caption -- user-uploaded media has no track
        <video key={m.id} src={m.url} controls preload="metadata" className="w-full rounded-md" />
      ))}

      {docs.map((m) => (
        <a
          key={m.id}
          href={m.url}
          target="_blank"
          rel="noreferrer"
          className="flex items-center gap-2 rounded-md border border-border bg-background px-3 py-2 text-sm text-text hover:border-accent"
        >
          <FileText size={16} className="shrink-0 text-muted" />
          <span className="truncate">{m.kind || "Document"}</span>
          <span className="ml-auto shrink-0 text-xs text-muted">{Math.round(m.size_bytes / 1024)} KB</span>
        </a>
      ))}
    </div>
  );
}
