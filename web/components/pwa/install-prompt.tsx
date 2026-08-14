"use client";

import { useEffect } from "react";
import { useUiStore } from "@/store/ui";

export function InstallPromptWatcher() {
  const setInstallPromptAvailable = useUiStore((state) => state.setInstallPromptAvailable);
  useEffect(() => {
    const handler = (event: Event) => {
      event.preventDefault();
      setInstallPromptAvailable(true);
    };
    window.addEventListener("beforeinstallprompt", handler);
    return () => window.removeEventListener("beforeinstallprompt", handler);
  }, [setInstallPromptAvailable]);
  return null;
}
