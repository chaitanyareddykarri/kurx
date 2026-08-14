import { redirect } from "next/navigation";

/// The organizer dashboard was an organization's home page — it opened onto one organization's modules
/// and needed an organization chosen before it could render at all. A person's home for hosting is now
/// their own event list (D-267), so this address just goes there.
export default function HostPage() {
  redirect("/workspace");
}
