"use client";

import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useCallback, useEffect, useRef, useState } from "react";
import { siteConfig } from "@/lib/site";

/// One server→client chat event, unwrapped from the versioned envelope (D-104).
///
/// The envelope is `{ v, type, roomId, id, payload }` on a single `chat` method, so new event types
/// ship server-first. Unknown types and unknown fields are ignored by contract — which is why
/// nothing here switches exhaustively on `type`.
export type ChatEvent = {
  type: string;
  roomId: string;
  id?: string | null;
  payload?: Record<string, unknown> | null;
};

export function parseChatEvent(raw: unknown): ChatEvent | null {
  if (!raw || typeof raw !== "object") return null;
  const map = raw as Record<string, unknown>;
  if (typeof map.type !== "string" || map.roomId == null) return null;
  return {
    type: map.type,
    roomId: String(map.roomId),
    id: map.id == null ? null : String(map.id),
    payload: (map.payload as Record<string, unknown>) ?? null
  };
}

/// Fetches the access token for the socket. The token is httpOnly, so it can only come from a
/// same-origin route handler; `accessTokenFactory` re-invokes this on every reconnect, which is what
/// lets a refreshed token be picked up without tearing the connection down (D-109).
async function fetchRealtimeToken(): Promise<string> {
  const res = await fetch("/api/realtime-token", { cache: "no-store" });
  if (!res.ok) throw new Error("realtime_unauthorized");
  const data = (await res.json()) as { accessToken?: string };
  if (!data.accessToken) throw new Error("realtime_unauthorized");
  return data.accessToken;
}

type UseChatHubOptions = {
  roomId: string | undefined;
  onEvent: (event: ChatEvent) => void;
  /// Called after the socket is re-established and the room re-joined. The room hook runs its
  /// `?after=` catch-up here, which is what recovers messages missed while disconnected.
  onReconnected: () => void;
};

/// Live chat updates.
///
/// Realtime is an optimisation, never the source of truth: a dead connection degrades the room to
/// manual refresh plus cursor sync and is never surfaced as "chat is broken". Gated behind the same
/// `NEXT_PUBLIC_ENABLE_SIGNALR` flag the rest of the app's realtime uses.
export function useChatHub({ roomId, onEvent, onReconnected }: UseChatHubOptions) {
  const [live, setLive] = useState(false);

  /// The live connection, so callers can invoke server methods (typing, heartbeat). Held in a ref
  /// rather than state: swapping it must not re-render the room.
  const connectionRef = useRef<HubConnection | undefined>(undefined);

  // Held in refs so a re-render that changes the callbacks does not tear down the socket.
  const onEventRef = useRef(onEvent);
  const onReconnectedRef = useRef(onReconnected);
  onEventRef.current = onEvent;
  onReconnectedRef.current = onReconnected;

  useEffect(() => {
    if (!roomId) return;
    if (process.env.NEXT_PUBLIC_ENABLE_SIGNALR !== "true") {
      setLive(false);
      return;
    }

    let connection: HubConnection | undefined;
    let cancelled = false;

    const start = async () => {
      try {
        connection = new HubConnectionBuilder()
          .withUrl(`${siteConfig.apiBaseUrl}/hubs/chat`, {
            accessTokenFactory: fetchRealtimeToken
          })
          .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
          .configureLogging(LogLevel.Warning)
          .build();

        connection.on("chat", (raw: unknown) => {
          const event = parseChatEvent(raw);
          if (event) onEventRef.current(event);
        });

        // withAutomaticReconnect restores the socket but NOT group membership — JoinRoom must be
        // re-invoked, and the server re-checks membership every time (D-017).
        connection.onreconnected(async () => {
          try {
            await connection?.invoke("JoinRoom", roomId);
            setLive(true);
            onReconnectedRef.current();
          } catch {
            // Membership may have been revoked while offline (banned, refunded). REST will say so.
            setLive(false);
          }
        });
        connection.onreconnecting(() => setLive(false));
        connection.onclose(() => setLive(false));

        await connection.start();
        await connection.invoke("JoinRoom", roomId);
        if (cancelled) return;
        connectionRef.current = connection;
        setLive(true);
      } catch {
        setLive(false);
      }
    };

    void start();

    return () => {
      cancelled = true;
      setLive(false);
      connectionRef.current = undefined;
      void connection?.stop();
    };
  }, [roomId]);

  /// Tells the server this user started or stopped typing (D-114).
  ///
  /// Fire-and-forget: a dropped typing event is cosmetic, and the receiver's own TTL clears the
  /// indicator anyway. Never worth surfacing an error for.
  const sendTyping = useCallback(async (id: string, isTyping: boolean) => {
    const connection = connectionRef.current;
    if (connection?.state !== "Connected") return;
    try {
      await connection.invoke("Typing", id, isTyping);
    } catch {
      // Cosmetic only.
    }
  }, []);

  /// Refreshes this connection's presence TTL.
  ///
  /// Not polling — presence *state* only ever arrives pushed. This exists so the server's TTL can
  /// stay short enough to recover from a crashed process without expiring a healthy idle tab.
  const heartbeat = useCallback(async () => {
    const connection = connectionRef.current;
    if (connection?.state !== "Connected") return;
    try {
      await connection.invoke("Heartbeat");
    } catch {
      // The next tick tries again; a missed beat is covered by the TTL margin.
    }
  }, []);

  return { live, sendTyping, heartbeat };
}
