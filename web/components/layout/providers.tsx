"use client";

import { ThemeProvider } from "next-themes";
import { NextIntlClientProvider } from "next-intl";
import type { AbstractIntlMessages } from "next-intl";
import { ReactNode } from "react";
import { ToastProvider } from "@kurx/ui";
import { QueryProvider } from "@/lib/query-client";
import { InstallPromptWatcher } from "@/components/pwa/install-prompt";

interface ProvidersProps {
  children: ReactNode;
  locale: string;
  messages: AbstractIntlMessages;
}

export function Providers({ children, locale, messages }: ProvidersProps) {
  return (
    <NextIntlClientProvider locale={locale} messages={messages}>
      <ThemeProvider attribute="class" defaultTheme="dark" enableSystem storageKey="kurx-theme">
        <QueryProvider>
          {/*
            Phase 8 built the toast system to close audit S1-4 and wired it into
            admin's shell only — on web `ToastProvider` was mounted nowhere, so
            `useToast()` resolved to the context's no-op default and every web
            call site would have announced nothing at all. Mounted at the root,
            not in `AppShell`, because public routes (`/u`, `/o`, `/e`) render
            outside that shell and their actions need the same channel.
          */}
          <ToastProvider>
            <InstallPromptWatcher />
            {children}
          </ToastProvider>
        </QueryProvider>
      </ThemeProvider>
    </NextIntlClientProvider>
  );
}
