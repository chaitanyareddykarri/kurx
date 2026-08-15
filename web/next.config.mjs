import withPWAInit from "@ducanh2912/next-pwa";
import createNextIntlPlugin from "next-intl/plugin";

const withNextIntl = createNextIntlPlugin("./i18n/request.ts");

const withPWA = withPWAInit({
  dest: "public",
  disable: process.env.NODE_ENV === "development",
  register: true,
  workboxOptions: {
    skipWaiting: true,
    // Take over already-open tabs, not just new ones. `skipWaiting` alone activates the new worker but
    // leaves existing clients controlled by the old one, so a tab open across a deploy keeps being served
    // the previous build's chunks. That is not merely stale UI: Server Action ids are per-build, so the
    // old bundle calls an action the running server no longer has, and the call fails in the browser
    // before any of our code runs — with nothing in either server log to explain it.
    clientsClaim: true,
    // Drop superseded precaches instead of accumulating one per deploy.
    cleanupOutdatedCaches: true,
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
  experimental: {
    serverActions: {
      // Participant lists are uploaded through a Server Action, and Next's default body limit is 1MB —
      // well under the 10MB the client offers and the API accepts. Those three numbers disagreeing meant
      // a large-but-legal spreadsheet was rejected at a boundary nobody had configured, before any of our
      // own validation ran (D-355, Phase 6). 12MB leaves room for multipart overhead on a 10MB file.
      bodySizeLimit: "12mb"
    }
  },
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
