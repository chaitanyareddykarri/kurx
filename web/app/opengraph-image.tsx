import { ImageResponse } from "next/og";
import { themeHex } from "@/lib/design-tokens";

export const dynamic = "force-dynamic";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function Image() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          background: themeHex.background,
          color: themeHex.text,
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: 72,
          border: `1px solid ${themeHex.border}`
        }}
      >
        <div style={{ color: themeHex.accent, fontSize: 28, fontWeight: 700 }}>Kurx</div>
        <div style={{ marginTop: 24, fontSize: 72, fontWeight: 800, letterSpacing: 0 }}>Desktop event operations</div>
        <div style={{ marginTop: 20, maxWidth: 900, fontSize: 32, color: themeHex.muted }}>
          Discover events, book tickets, verify certificates, and manage organizer workflows.
        </div>
      </div>
    ),
    size
  );
}
