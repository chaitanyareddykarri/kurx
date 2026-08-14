"use client";

import { create } from "zustand";

type UiState = {
  sidebarOpen: boolean;
  installPromptAvailable: boolean;
  setSidebarOpen: (open: boolean) => void;
  setInstallPromptAvailable: (available: boolean) => void;
};

export const useUiStore = create<UiState>((set) => ({
  sidebarOpen: false,
  installPromptAvailable: false,
  setSidebarOpen: (sidebarOpen) => set({ sidebarOpen }),
  setInstallPromptAvailable: (installPromptAvailable) => set({ installPromptAvailable })
}));
