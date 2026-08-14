import { redirect } from "next/navigation";

// Attendees are now viewed per-event inside the event workspace (/host/events/[id]/attendees).
export default function AttendeesRedirect() {
  redirect("/workspace");
}
