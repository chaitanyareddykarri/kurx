import { vi } from "vitest";
import "@testing-library/jest-dom/vitest";

// The realtime flag is off in tests: the hub is exercised through its pure parser and a mocked
// module, never a real socket.
process.env.NEXT_PUBLIC_ENABLE_SIGNALR = "false";

// Server actions are stubbed for every suite, not per-file. Rendering any chat component pulls in
// components/chat/attachment-view -> lib/chat-actions ("use server") -> lib/session -> lib/api, which
// calls React's `cache()` at module scope. `cache` only exists under the react-server export
// condition, so in jsdom it is undefined and the import throws "cache is not a function" — which
// vitest reports as a suite that collected 0 tests rather than as a failing assertion.
// chat-attachments.test.tsx already mocked this file locally; doing it here instead means a new chat
// test cannot silently lose its whole suite by forgetting to.
vi.mock("@/lib/chat-actions", () => ({
  attachmentUrlAction: vi.fn(async () => ""),
  presignChatAttachmentAction: vi.fn(async () => ({})),
  confirmChatAttachmentAction: vi.fn(async () => ({})),
  listMyChatsAction: vi.fn(async () => []),
  sendMessageAction: vi.fn(async () => ({})),
}));
