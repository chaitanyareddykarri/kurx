"use client";

import { useTransition } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@kurx/ui";
import { logoutAction } from "@/lib/auth-actions";

export function SignOutButton() {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <Button
      variant="secondary"
      disabled={pending}
      onClick={() =>
        startTransition(async () => {
          await logoutAction();
          router.replace("/login");
        })
      }
    >
      Sign out
    </Button>
  );
}
