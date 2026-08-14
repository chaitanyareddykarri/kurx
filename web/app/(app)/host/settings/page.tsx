import { redirect } from "next/navigation";

// Kurx has no organization accounts, so there is no "organization settings". Settings are user-scoped —
// org records are edited only by admins. Editing an org you represent is not a user capability.
export default function HostSettingsRedirect() {
  redirect("/settings");
}
