import Link from "next/link";
import { LifeBuoy, Mail, MessageSquare } from "lucide-react";
import { Card } from "@/components/ui/card";

export const metadata = { title: "Help & support · Kurx" };

/**
 * Help & support.
 *
 * Mobile has had this since D-263; web had nothing, so a browser-only user had no route to a human
 * at all. It links rather than embeds a form: there is no support-ticket endpoint on the platform,
 * and a form posting nowhere is worse than an address that works.
 */
export default function HelpSupportPage() {
  return (
    <div className="space-y-lg">
      <div>
        <h1 className="text-2xl font-semibold text-text">Help &amp; support</h1>
        <p className="mt-1 text-sm text-muted">Something not working, or not making sense? Ask us.</p>
      </div>

      <Card>
        <ul className="space-y-md">
          <li className="flex items-start gap-md">
            <Mail size={18} aria-hidden className="mt-0.5 shrink-0 text-muted" />
            <span>
              <span className="block text-body font-medium text-text">Contact us</span>
              {/* /contact, not a mailto: there is no support address in siteConfig and inventing one
                  would publish an inbox nobody reads. */}
              <Link href="/contact" className="text-sm text-accent-text hover:underline">
                Send us a message
              </Link>
            </span>
          </li>
          <li className="flex items-start gap-md">
            <MessageSquare size={18} aria-hidden className="mt-0.5 shrink-0 text-muted" />
            <span>
              <span className="block text-body font-medium text-text">Support centre</span>
              <Link href="/support" className="text-sm text-accent-text hover:underline">
                Browse common questions
              </Link>
            </span>
          </li>
          <li className="flex items-start gap-md">
            <LifeBuoy size={18} aria-hidden className="mt-0.5 shrink-0 text-muted" />
            <span>
              <span className="block text-body font-medium text-text">Account recovery</span>
              <Link href="/recover" className="text-sm text-accent-text hover:underline">
                Lost access to your phone or device
              </Link>
            </span>
          </li>
        </ul>
      </Card>
    </div>
  );
}
