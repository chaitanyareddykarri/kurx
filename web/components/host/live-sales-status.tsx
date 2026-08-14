"use client";

import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { Radio } from "lucide-react";
import { useEffect, useState } from "react";
import { siteConfig } from "@/lib/site";

export function LiveSalesStatus() {
  const [state, setState] = useState<HubConnectionState | "idle">("idle");

  useEffect(() => {
    if (process.env.NEXT_PUBLIC_ENABLE_SIGNALR !== "true") {
      setState("idle");
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${siteConfig.apiBaseUrl}/hubs/sales`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.start().then(() => setState(connection.state)).catch(() => setState("Disconnected" as HubConnectionState));
    connection.onreconnecting(() => setState(connection.state));
    connection.onreconnected(() => setState(connection.state));
    connection.onclose(() => setState(connection.state));

    return () => {
      void connection.stop();
    };
  }, []);

  return (
    <span className="inline-flex items-center gap-2 rounded-full border border-success/30 px-3 py-1 text-sm text-success">
      <Radio size={14} /> {state}
    </span>
  );
}
