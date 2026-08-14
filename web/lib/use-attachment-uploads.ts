"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { confirmAttachmentAction, presignAttachmentAction } from "@/lib/chat-actions";
import {
  createPendingUpload,
  extractErrorCode,
  releasePendingUpload,
  uploadToPresignedUrl,
  type PendingUpload
} from "@/lib/chat-upload";

/// Drives the attachment upload queue for one room.
///
/// Uploads run **sequentially**, not in parallel: a chat composer is not a bulk uploader, and a
/// serial queue keeps progress legible, bounds memory, and means a failure stops at one file instead
/// of scattering across several.
///
/// Deliberately plain React state rather than React Query. The chat feature already keeps its state
/// in `useChatRoom`; adding a second state system for uploads would put two sources of truth in one
/// screen for no benefit. The mutation semantics that matter — sequencing, cancellation, retry,
/// reconciliation — are tested directly against this hook.
export function useAttachmentUploads(roomId: string | undefined) {
  const [uploads, setUploads] = useState<PendingUpload[]>([]);

  // Abort controllers by localId, so cancel can reach an in-flight XHR.
  const controllers = useRef(new Map<string, AbortController>());
  // Guards against a double-fire (double-click, duplicate drop event) starting the same file twice.
  const running = useRef(false);
  const queueRef = useRef<PendingUpload[]>([]);
  queueRef.current = uploads;
  // Ids already handed to runOne. See the note on `drain`.
  const started = useRef(new Set<string>());

  const update = useCallback((localId: string, patch: Partial<PendingUpload>) => {
    setUploads((prev) => prev.map((u) => (u.localId === localId ? { ...u, ...patch } : u)));
  }, []);

  const runOne = useCallback(
    async (upload: PendingUpload) => {
      if (!roomId) return;

      const controller = new AbortController();
      controllers.current.set(upload.localId, controller);
      update(upload.localId, { status: "uploading", progress: 0, error: undefined });

      try {
        // 1. Presign — server action, so the session token never reaches the browser.
        const ticket = await presignAttachmentAction(
          roomId,
          upload.fileName,
          upload.contentType,
          upload.sizeBytes
        );

        // 2. PUT — browser-direct to the presigned URL. No cookies, no Authorization, no
        //    interceptor: the URL carries its own signature.
        await uploadToPresignedUrl(ticket.url, upload.file, ticket.headers, {
          onProgress: (fraction) => update(upload.localId, { progress: fraction }),
          signal: controller.signal
        });

        // 3. Confirm — where the server validates what actually arrived.
        update(upload.localId, { status: "confirming", progress: 1 });
        const attachment = await confirmAttachmentAction(roomId, ticket.key);

        update(upload.localId, { status: "done", attachmentId: attachment.id, progress: 1 });
      } catch (error) {
        const code = extractErrorCode(error);
        update(upload.localId, {
          status: code === "cancelled" ? "cancelled" : "failed",
          error: code ?? "network"
        });
      } finally {
        controllers.current.delete(upload.localId);
      }
    },
    [roomId, update]
  );

  /// Drains the queue one file at a time. Re-entrant calls return immediately, which is what stops a
  /// repeated user action from uploading the same file twice.
  ///
  /// Progress is tracked by a set of already-started ids rather than by re-reading status from the
  /// queue ref. The ref only refreshes on re-render, so a status written mid-loop is not visible
  /// yet — reading it back would re-select the same file forever.
  const drain = useCallback(async () => {
    if (running.current) return;
    running.current = true;
    try {
      for (;;) {
        const next = queueRef.current.find(
          (u) => u.status === "queued" && !started.current.has(u.localId)
        );
        if (!next) break;
        started.current.add(next.localId);
        await runOne(next);
      }
    } finally {
      running.current = false;
    }
  }, [runOne]);

  const add = useCallback(
    (files: readonly File[]) => {
      if (files.length === 0) return;
      const pending = files.map(createPendingUpload);
      setUploads((prev) => [...prev, ...pending]);
      // Queued state is applied synchronously above; drain picks them up on the next tick.
      queueRef.current = [...queueRef.current, ...pending];
      void drain();
    },
    [drain]
  );

  const cancel = useCallback((localId: string) => {
    controllers.current.get(localId)?.abort();
    controllers.current.delete(localId);
  }, []);

  const retry = useCallback(
    (localId: string) => {
      started.current.delete(localId);
      update(localId, { status: "queued", error: undefined, progress: 0 });
      queueRef.current = queueRef.current.map((u) =>
        u.localId === localId ? { ...u, status: "queued" as const, error: undefined } : u
      );
      void drain();
    },
    [drain, update]
  );

  const remove = useCallback((localId: string) => {
    controllers.current.get(localId)?.abort();
    controllers.current.delete(localId);
    started.current.delete(localId);
    setUploads((prev) => {
      const target = prev.find((u) => u.localId === localId);
      if (target) releasePendingUpload(target);
      return prev.filter((u) => u.localId !== localId);
    });
  }, []);

  /// Called once the message has been sent — the uploads have been reconciled into it.
  const clear = useCallback(() => {
    for (const controller of controllers.current.values()) controller.abort();
    controllers.current.clear();
    started.current.clear();
    setUploads((prev) => {
      prev.forEach(releasePendingUpload);
      return [];
    });
  }, []);

  // Abort anything in flight and release object URLs when the room closes.
  useEffect(
    () => () => {
      for (const controller of controllers.current.values()) controller.abort();
      controllers.current.clear();
      queueRef.current.forEach(releasePendingUpload);
    },
    []
  );

  return { uploads, add, cancel, retry, remove, clear };
}
