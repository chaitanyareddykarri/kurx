"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { getMe } from "@/lib/api";

const accessCookie = "kurx_access";
const refreshCookie = "kurx_refresh";

export async function saveSession(accessToken: string, refreshTokenValue: string) {
  cookies().set(accessCookie, accessToken, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/"
  });
  cookies().set(refreshCookie, refreshTokenValue, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/"
  });
}

export async function clearSession() {
  cookies().delete(accessCookie);
  cookies().delete(refreshCookie);
}

export async function currentSession() {
  const accessToken = cookies().get(accessCookie)?.value;
  const refreshTokenValue = cookies().get(refreshCookie)?.value;
  if (!accessToken || !refreshTokenValue) return null;

  try {
    const me = await getMe(accessToken);
    return { accessToken, refreshToken: refreshTokenValue, me };
  } catch {
    // Token refresh runs in middleware.ts before the render, where cookies are mutable. Never write
    // cookies here: Next forbids it during a Server Component render ("Cookies can only be modified in
    // a Server Action or Route Handler"). A failure now just means "not authenticated this request".
    return null;
  }
}

export async function requireSession() {
  const session = await currentSession();
  if (!session) redirect("/?login=required#login");
  if (session.me.needs_onboarding) redirect("/register");
  return session;
}
