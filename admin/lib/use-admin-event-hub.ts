"use client";

import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useEffect, useRef, useState } from "react";
import { siteConfig } from "@/lib/site";

/// D-186: the admin event workspace's live feed. Reuses the existing ScanHub (`event:{id}` group,
/// "scan" method) and SalesHub (`org:{id}` group, "sale"/"analytics" methods) — no new hub, no new
/// broadcast method. Both hubs' group-join now honours the `kurx_admin` claim bypass (ScanHub.cs/
/// SalesHub.cs), so a SuperAdmin session can join any event's/org's group, not just its own.
export type AdminLiveEvent =
  | { kind: "scan"; ticketId: string; eventId: string; scannedAt: string; eligibilityFlag: string | null }
  | { kind: "sale" | "analytics"; eventId: string; amountPaise: number; at: string };

async function fetchRealtimeToken(): Promise<string> {
  const res = await fetch("/api/realtime-token", { cache: "no-store" });
  if (!res.ok) throw new Error("realtime_unauthorized");
  const data = (await res.json()) as { accessToken?: string };
  if (!data.accessToken) throw new Error("realtime_unauthorized");
  return data.accessToken;
}

function buildHub(path: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${siteConfig.apiBaseUrl}${path}`, { accessTokenFactory: fetchRealtimeToken })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();
}

/// Live scan + sales feed for one event, scoped to its own org. Realtime is an optimisation, never
/// the source of truth — a dead connection just means the workspace falls back to its already-loaded
/// snapshot; it's never surfaced as broken. Gated behind the same `NEXT_PUBLIC_ENABLE_SIGNALR` flag
/// web's chat/sales sockets already use.
export function useAdminEventHub(eventId: string | undefined, orgId: string | undefined, onEvent: (e: AdminLiveEvent) => void) {
  const [live, setLive] = useState(false);
  /*
   * Whether a live feed is even expected here.
   *
   * "Realtime is an optimisation, never the source of truth" is right about the *data* and wrong
   * about the *operator*. On a check-in monitor during an event, a dead socket and a quiet gate look
   * identical — the workspace showed a "live" badge when connected and **nothing at all** when not,
   * so an operator watching an empty feed could not tell "nobody has arrived in ten minutes" from
   * "the feed died ten minutes ago", and that is a decision they make on the door.
   *
   * Distinguished from the flag being off, where no live feed is promised and so none is missed.
   */
  const enabled = process.env.NEXT_PUBLIC_ENABLE_SIGNALR === "true" && Boolean(eventId) && Boolean(orgId);
  const onEventRef = useRef(onEvent);
  onEventRef.current = onEvent;

  useEffect(() => {
    if (!eventId || !orgId || process.env.NEXT_PUBLIC_ENABLE_SIGNALR !== "true") {
      setLive(false);
      return;
    }

    let cancelled = false;
    const scan = buildHub("/hubs/scan");
    const sales = buildHub("/hubs/sales");
    let scanLive = false;
    let salesLive = false;
    const setCombinedLive = () => setLive(scanLive && salesLive);

    scan.on("scan", (raw: { ticket_id: string; event_id: string; scanned_at: string; eligibility_flag: string | null }) => {
      onEventRef.current({ kind: "scan", ticketId: raw.ticket_id, eventId: raw.event_id, scannedAt: raw.scanned_at, eligibilityFlag: raw.eligibility_flag });
    });
    const onSale = (kind: "sale" | "analytics") => (raw: { event_id: string; amount_paise: number; at: string }) =>
      onEventRef.current({ kind, eventId: raw.event_id, amountPaise: raw.amount_paise, at: raw.at });
    sales.on("sale", onSale("sale"));
    sales.on("analytics", onSale("analytics"));

    scan.onreconnected(async () => {
      try { await scan.invoke("JoinEvent", eventId); scanLive = true; } catch { scanLive = false; }
      setCombinedLive();
    });
    sales.onreconnected(async () => {
      try { await sales.invoke("JoinOrg", orgId); salesLive = true; } catch { salesLive = false; }
      setCombinedLive();
    });
    scan.onreconnecting(() => { scanLive = false; setCombinedLive(); });
    sales.onreconnecting(() => { salesLive = false; setCombinedLive(); });
    scan.onclose(() => { scanLive = false; setCombinedLive(); });
    sales.onclose(() => { salesLive = false; setCombinedLive(); });

    void (async () => {
      try {
        await scan.start();
        await scan.invoke("JoinEvent", eventId);
        if (cancelled) return;
        scanLive = true;
      } catch { scanLive = false; }
      try {
        await sales.start();
        await sales.invoke("JoinOrg", orgId);
        if (cancelled) return;
        salesLive = true;
      } catch { salesLive = false; }
      if (!cancelled) setCombinedLive();
    })();

    return () => {
      cancelled = true;
      setLive(false);
      void scan.stop();
      void sales.stop();
    };
  }, [eventId, orgId]);

  return { live, enabled };
}
