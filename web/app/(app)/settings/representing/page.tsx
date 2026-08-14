import { redirect } from "next/navigation";

// Representing is not an account setting: it is the authority to act for an organization, which
// belongs with hosting, not with how your account behaves. `/host/representing` is the surface that
// owns it. Redirects rather than deletes so existing links keep working.
export default function RepresentingSettingsRedirect() {
  redirect("/host/representing");
}
