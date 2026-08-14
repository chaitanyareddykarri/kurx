import type { MetadataRoute } from "next";
import { themeHex } from "@/lib/design-tokens";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Kurx",
    short_name: "Kurx",
    description: "Discover events, manage tickets, and run organizer workflows with Kurx.",
    start_url: "/discover",
    display: "standalone",
    background_color: themeHex.background,
    theme_color: themeHex.accent,
    categories: ["business", "productivity", "events"],
    icons: [{ src: "/icon", sizes: "512x512", type: "image/png", purpose: "maskable" }]
  };
}
