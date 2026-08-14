import type { Metadata } from "next";

export const siteConfig = {
  name: "Kurx",
  url: process.env.NEXT_PUBLIC_SITE_URL ?? "https://kurx.app",
  description:
    "Kurx helps people discover events, book tickets, verify certificates, and gives organizers a desktop-grade event operations platform.",
  appStoreUrl: process.env.NEXT_PUBLIC_IOS_APP_URL ?? "https://apps.apple.com/app/kurx",
  playStoreUrl: process.env.NEXT_PUBLIC_ANDROID_APP_URL ?? "https://play.google.com/store/apps/details?id=app.kurx",
  // The dedicated admin console (D-195) — former web/host/admin/* pages redirect here.
  adminUrl: process.env.NEXT_PUBLIC_ADMIN_URL ?? "http://localhost:3001",
  // The browser reaches the API via the public host URL (baked at build); server-side renders run
  // inside the container and must use the internal service URL (API_INTERNAL_URL, read at runtime).
  apiBaseUrl:
    typeof window === "undefined"
      ? process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080"
      : process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080"
};

export const publicPages = [
  { href: "/", label: "Home" },
  { href: "/features", label: "Features" },
  { href: "/pricing", label: "Pricing" },
  { href: "/about", label: "About" },
  { href: "/support", label: "Support" },
  { href: "/contact", label: "Contact" },
  { href: "/blog", label: "Blog" },
  { href: "/terms", label: "Terms" },
  { href: "/privacy", label: "Privacy" }
] as const;

type SeoInput = {
  title: string;
  description?: string;
  path?: string;
  image?: string;
};

export function createMetadata({ title, description = siteConfig.description, path = "/", image = "/opengraph-image" }: SeoInput): Metadata {
  const url = new URL(path, siteConfig.url);
  return {
    metadataBase: new URL(siteConfig.url),
    title: title === siteConfig.name ? title : `${title} | ${siteConfig.name}`,
    description,
    alternates: { canonical: url.toString() },
    openGraph: {
      title,
      description,
      url: url.toString(),
      siteName: siteConfig.name,
      images: [{ url: image, width: 1200, height: 630, alt: title }],
      locale: "en_US",
      type: "website"
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
      images: [image]
    },
    appleWebApp: {
      capable: true,
      title: siteConfig.name,
      statusBarStyle: "black-translucent"
    },
    other: {
      "apple-itunes-app": `app-id=${process.env.NEXT_PUBLIC_IOS_APP_ID ?? "0000000000"}, app-argument=kurx://open`
    }
  };
}

export function jsonLd(type: string, data: Record<string, unknown>) {
  return {
    __html: JSON.stringify({
      "@context": "https://schema.org",
      "@type": type,
      ...data
    })
  };
}
