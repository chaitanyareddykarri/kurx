/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Shared design system (@kurx/ui) is TS source consumed from the workspace.
  transpilePackages: ["@kurx/ui"],
  // Internal tool — never index, never frame. Real network isolation (IP allowlist /
  // VPN / SSO) is enforced by middleware.ts and the deploy environment.
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Frame-Options", value: "DENY" },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "no-referrer" },
          { key: "X-Robots-Tag", value: "noindex, nofollow" }
        ]
      }
    ];
  }
};

export default nextConfig;
