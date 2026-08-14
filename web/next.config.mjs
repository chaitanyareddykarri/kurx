import withPWAInit from "@ducanh2912/next-pwa";
import createNextIntlPlugin from "next-intl/plugin";

const withNextIntl = createNextIntlPlugin("./i18n/request.ts");

const withPWA = withPWAInit({
  dest: "public",
  disable: process.env.NODE_ENV === "development",
  register: true,
  workboxOptions: {
    skipWaiting: true,
    runtimeCaching: [
      {
        urlPattern: /^https?.*\/v1\/me/,
        handler: "NetworkFirst",
        options: {
          cacheName: "kurx-session",
          expiration: { maxEntries: 16, maxAgeSeconds: 60 * 60 }
        }
      },
      {
        urlPattern: /^https?.*\/v1\/.*tickets/,
        handler: "NetworkFirst",
        options: {
          cacheName: "kurx-offline-tickets",
          expiration: { maxEntries: 64, maxAgeSeconds: 60 * 60 * 24 * 7 }
        }
      }
    ]
  }
});

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Shared design system is TS source consumed from the workspace — Next must transpile it.
  transpilePackages: ["@kurx/ui"],
  images: {
    remotePatterns: [
      { protocol: "https", hostname: "**" },
      { protocol: "http", hostname: "localhost" }
    ]
  }
};

export default withNextIntl(withPWA(nextConfig));
