"use client";

import { logoutAction } from "@/lib/actions";
import { Button } from "@/components/ui/button";

export function LogoutButton() {
  return (
    <form action={logoutAction}>
      <Button type="submit" variant="secondary">Log out</Button>
    </form>
  );
}
