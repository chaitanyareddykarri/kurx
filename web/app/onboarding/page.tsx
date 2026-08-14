import { redirect } from "next/navigation";

/**
 * Profile completion is now the "complete profile" step of the registration ceremony (Phase 2D).
 * This route is kept only so existing links/redirects land in the right place.
 */
export default function OnboardingPage() {
  redirect("/register");
}
