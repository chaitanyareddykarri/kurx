import { readdirSync, readFileSync, statSync } from "node:fs";
import { resolve } from "node:path";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { PostMediaGrid } from "@/components/posts/post-media-grid";
import type { PostMedia } from "@/lib/posts-api";

/**
 * Phase 46 — an image that carries meaning has to be named.
 *
 * `alt=""` is a claim that an image is decorative and a screen reader should skip it. On the post
 * media grid it was applied to the images that *are* the post, so a reader was told a post existed
 * and never that it had pictures.
 */
// Six, because the grid shows four and covers the last with a "+2" badge.
const IMAGES: PostMedia[] = Array.from({ length: 6 }, (_, i) => ({
  id: String(i + 1),
  kind: "image",
  url: `https://cdn.example/${i}.jpg`,
  content_type: "image/jpeg",
  size_bytes: 1024,
  sort: i
}));

describe("post media", () => {
  it("names each image without inventing what is in it", () => {
    render(<PostMediaGrid media={IMAGES} />);
    // There is no caption or description on media, so the alt says only what is known.
    expect(screen.getByAltText("Image 1 of 6 in this post")).toBeInTheDocument();
  });

  it("counts the images that exist, not the ones that fit", () => {
    // The grid shows four and covers the last with a "+2" badge. A reader must still learn there
    // are six, because the badge is a visual device and is hidden from them.
    render(<PostMediaGrid media={IMAGES} />);
    expect(screen.getAllByRole("img").length).toBeLessThan(IMAGES.length);
    expect(screen.getAllByAltText(/of 6 in this post/).length).toBeGreaterThan(0);
  });
});

describe("no image is silently marked decorative", () => {
  it("uses alt=\"\" nowhere without saying why", () => {
    const walk = (d: string): string[] =>
      readdirSync(d).flatMap((e) => {
        const full = resolve(d, e);
        return statSync(full).isDirectory() ? walk(full) : /\.tsx$/.test(e) ? [full] : [];
      });
    const offenders: string[] = [];
    for (const f of [...walk(resolve(__dirname, "..", "components")), ...walk(resolve(__dirname, "..", "app"))]) {
      readFileSync(f, "utf8").split("\n").forEach((line, i) => {
        const t = line.trim();
        if (/^(\/\/|\*|\{?\/\*)/.test(t)) return;
        // `aria-hidden` alongside is the explicit "this is a backdrop" decision; a bare `alt=""`
        // is the accidental one.
        if (/alt=""/.test(line) && !line.includes("aria-hidden")) {
          offenders.push(`${f.split("/web/")[1]}:${i + 1}`);
        }
      });
    }
    // An empty alt is legitimate for a genuinely decorative image, but it has to be a decision:
    // pair it with `aria-hidden` so the intent is on the element rather than inferred from silence.
    expect(offenders).toEqual([]);
  });
});
