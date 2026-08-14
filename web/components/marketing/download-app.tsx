import { QrCode } from "lucide-react";
import { LinkButton } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { siteConfig } from "@/lib/site";

export function DownloadApp({ deepLink = "kurx://open" }: { deepLink?: string }) {
  return (
    // Same band rhythm as every other section on the landing page (D-291): one container, one
    // `py-16 sm:py-20`, the eyebrow → h2 → lead header pattern.
    <section id="download-app" className="border-b border-border bg-surface">
      <div className="container-shell grid gap-10 py-16 sm:py-20 md:grid-cols-[1fr_320px] md:items-center">
        <div>
          <p className="text-caption font-semibold text-accent-text">Mobile app</p>
          <h2 className="mt-2 max-w-2xl text-3xl font-semibold tracking-tight text-text sm:text-4xl">Scan, check in, and carry tickets faster on the Kurx app.</h2>
          <p className="mt-4 max-w-2xl text-body-lg leading-8 text-muted">
            Desktop keeps the full organizer workspace available. The mobile app adds QR scanning, offline scan queues, and deep links for tickets, certificates, and events.
          </p>
          <div className="mt-8 flex flex-wrap gap-3">
            <LinkButton href={siteConfig.playStoreUrl}>Download Android App</LinkButton>
            <LinkButton href={siteConfig.appStoreUrl} variant="secondary">Download iOS App</LinkButton>
            <LinkButton href={deepLink} variant="ghost">Continue in Browser</LinkButton>
          </div>
        </div>
        <Card className="grid place-items-center text-center">
          <QrCode size={112} className="text-accent" aria-hidden />
          <p className="mt-3 text-sm text-muted">QR code for mobile download</p>
        </Card>
      </div>
    </section>
  );
}
