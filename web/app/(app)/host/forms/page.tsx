import { redirect } from "next/navigation";

// Registration forms are the custom fields on a ticket type — managed in the event workspace's Tickets tab.
export default function FormsRedirect() {
  redirect("/workspace");
}
