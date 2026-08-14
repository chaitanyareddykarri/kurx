import type { Metadata, Viewport } from "next";
// Design tokens first (single source of truth, shared with admin), then app globals.
import "@kurx/ui/styles/tokens.css";
import "./globals.css";
import { Providers } from "@/components/layout/providers";
import { themeHex } from "@/lib/design-tokens";
import { createMetadata } from "@/lib/site";
import { getLocale, getMessages } from "next-intl/server";
import type { AbstractIntlMessages } from "next-intl";

export const metadata: Metadata = createMetadata({ title: "Kurx" });

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 5,
  themeColor: themeHex.accent
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const locale = await getLocale();
  const messages = await getMessages();

  return (
    <html lang={locale} suppressHydrationWarning>
      <body>
        <Providers locale={locale} messages={messages as AbstractIntlMessages}>
          {children}
        </Providers>
      </body>
    </html>
  );
}
