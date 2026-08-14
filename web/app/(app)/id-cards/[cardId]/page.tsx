import { notFound } from "next/navigation";
import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { getIdCard } from "@/lib/api";
import { requireSession } from "@/lib/session";
import { IdCardEditor } from "@/components/id-cards/id-card-editor";

export const metadata = { title: "Edit ID Card" };

export default async function EditIdCardPage({ params }: { params: Promise<{ cardId: string }> }) {
  const { cardId } = await params;
  const session = await requireSession();

  // The API already collapses "not yours" to 404 (D-018), so there is no separate forbidden branch to
  // render here — a card the caller may not see does not exist as far as this page is concerned.
  let card;
  try {
    card = await getIdCard(session.accessToken, cardId);
  } catch {
    notFound();
  }

  // Phone, email and date of birth are the holder's own profile values. They are passed to the preview
  // rather than re-fetched inside it, and are never sent back on save: the card prints them, the
  // editor does not own them, and changing them belongs in profile settings (D-331).
  const me = session.me;

  return (
    <div className="space-y-6">
      <div>
        <Link href="/id-cards" className="inline-flex items-center gap-1 text-sm text-muted hover:text-text">
          <ChevronLeft size={14} aria-hidden /> ID Cards
        </Link>
        <h1 className="mt-2 text-3xl font-semibold text-text">Edit ID card</h1>
        <p className="mt-2 text-sm text-muted">
          Changes here affect this card only. Your profile is untouched.
        </p>
      </div>

      <IdCardEditor
        card={card}
        holderPhone={me.phone ?? null}
        holderEmail={me.email ?? null}
        holderDob={me.date_of_birth ?? null}
      />
    </div>
  );
}
