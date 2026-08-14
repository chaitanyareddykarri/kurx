import { redirect } from "next/navigation";

// Certificates are now issued per-event inside the event workspace (/host/events/[id]/certificates).
export default function CertificatesRedirect() {
  redirect("/workspace");
}
