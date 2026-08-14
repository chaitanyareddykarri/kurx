import type { MetadataRoute } from "next";
import { publicPages, siteConfig } from "@/lib/site";

export default function sitemap(): MetadataRoute.Sitemap {
  return publicPages.map((page) => ({
    url: `${siteConfig.url}${page.href}`,
    lastModified: new Date(),
    changeFrequency: page.href === "/" ? "daily" : "weekly",
    priority: page.href === "/" ? 1 : 0.7
  }));
}
