import { redirect } from "next/navigation";

// Tickets are now managed per-event inside the event workspace (/host/events/[id]/tickets).
export default function TicketsRedirect() {
  redirect("/workspace");
}
