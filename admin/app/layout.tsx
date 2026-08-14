import type { Metadata } from "next";
// Design tokens first (single source of truth, shared with web), then app globals.
import "@kurx/ui/styles/tokens.css";
import "./globals.css";
import { Providers } from "@/components/providers";

export const metadata: Metadata = {
  title: "Kurx Admin",
  description: "Kurx internal platform admin console",
  robots: { index: false, follow: false },
  icons: { icon: "/favicon.svg" }
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
