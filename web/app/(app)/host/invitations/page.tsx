import { redirect } from "next/navigation";

// Invitations are now managed per-event inside the event workspace (/host/events/[id]/invitations).
export default function InvitationsRedirect() {
  redirect("/workspace");
}
