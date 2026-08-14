import { ImageResponse } from "next/og";
import { themeHex } from "@/lib/design-tokens";

export const dynamic = "force-dynamic";
export const size = { width: 512, height: 512 };
export const contentType = "image/png";

export default function Icon() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          background: themeHex.background,
          color: themeHex.accent,
          fontSize: 220,
          fontWeight: 900
        }}
      >
        K
      </div>
    ),
    size
  );
}
