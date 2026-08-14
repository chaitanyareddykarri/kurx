"use client";

/**
 * Catches errors thrown in the root layout itself. Must render its own <html>/<body>, and no
 * stylesheet is guaranteed to have loaded — so the dark palette's `background`, `text`,
 * `border-strong` and `surface` are written literally. If the palette changes, this changes with it.
 *
 * These were the pre-D-286 GitHub-dark values until D-288; nothing compares this file to the
 * tokens, which is how it stayed stale through a whole palette change.
 */
export default function GlobalError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <html lang="en">
      <body style={{ fontFamily: "ui-sans-serif, system-ui, sans-serif", background: "#0A0A0A", color: "#FFFFFF" }}>
        <div style={{ display: "grid", minHeight: "100vh", placeItems: "center", padding: 24 }}>
          <div style={{ textAlign: "center" }}>
            <p style={{ marginBottom: 12 }}>The admin console failed to start.</p>
            <button
              onClick={reset}
              style={{
                border: "1px solid #64748B",
                background: "#111827",
                color: "#FFFFFF",
                borderRadius: 6,
                padding: "8px 16px",
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
