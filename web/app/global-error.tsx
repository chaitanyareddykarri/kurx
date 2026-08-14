"use client";

/**
 * Catches errors thrown in the root layout itself, where no styling or provider is available —
 * hence the inline styles and the bare `<html>`/`<body>`, which Next requires here.
 *
 * The values are the DARK palette's `background`, `text`, `muted`, `border-strong` and `surface`
 * written literally, because a stylesheet is exactly what is not guaranteed to have loaded at this
 * point — and dark is `:root`, so it is what the app boots into. `mobile/test/features/
 * palette_discipline_test.dart`'s reasoning does not reach this file, but the same discipline
 * applies: if the palette changes, this changes with it.
 */
export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <html lang="en">
      <body style={{ fontFamily: "ui-sans-serif, system-ui, sans-serif", background: "#0A0A0A", color: "#FFFFFF", margin: 0 }}>
        <div style={{ display: "grid", minHeight: "100vh", placeItems: "center", padding: 24 }}>
          <div style={{ textAlign: "center", maxWidth: 380 }}>
            <h1 style={{ fontSize: 20, fontWeight: 600, marginBottom: 8 }}>Kurx didn&apos;t load</h1>
            <p style={{ fontSize: 14, color: "#9CA3AF", marginBottom: 16 }}>
              Something went wrong on our side. Reloading usually fixes it.
            </p>
            <button
              onClick={reset}
              style={{
                minHeight: 44,
                border: "1px solid #64748B",
                background: "#111827",
                color: "#FFFFFF",
                borderRadius: 6,
                padding: "0 20px",
                fontSize: 14,
                cursor: "pointer"
              }}
            >
              Reload
            </button>
          </div>
        </div>
      </body>
    </html>
  );
}
