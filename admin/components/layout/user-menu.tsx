"use client";

import { useState, useTransition } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { KeyRound, LogOut, ShieldCheck } from "lucide-react";
import { logoutAction } from "@/lib/auth-actions";
import { ROLE_LABELS, type PlatformRole } from "@/lib/roles";

export function UserMenu({ name, roles }: { name: string; roles: PlatformRole[] }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [signingOut, setSigningOut] = useState(false);

  function signOut() {
    setSigningOut(true);
    startTransition(async () => {
      await logoutAction();
      router.replace("/login");
    });
  }

  const primaryRole = roles.includes("SuperAdmin") ? "SuperAdmin" : roles[0];

  return (
    <div className="flex items-center gap-2 sm:gap-3">
      <div className="hidden text-right sm:block">
        <p className="text-sm font-medium text-text">{name}</p>
        {primaryRole ? <p className="text-xs text-muted">{ROLE_LABELS[primaryRole]}</p> : null}
      </div>
      <Link
        href="/security"
        className="grid h-10 w-10 place-items-center rounded-md border border-border bg-surface text-muted transition hover:bg-elevated hover:text-text"
        aria-label="Security"
        title="Security"
      >
        <ShieldCheck size={17} />
      </Link>
      <Link
        href="/account"
        className="grid h-10 w-10 place-items-center rounded-md border border-border bg-surface text-muted transition hover:bg-elevated hover:text-text"
        aria-label="Account & password"
        title="Account & password"
      >
        <KeyRound size={17} />
      </Link>
      <button
        type="button"
        onClick={signOut}
        disabled={pending || signingOut}
        className="grid h-10 w-10 place-items-center rounded-md border border-border bg-surface text-muted transition hover:bg-elevated hover:text-text disabled:opacity-50"
        aria-label="Sign out"
        title="Sign out"
      >
        <LogOut size={17} />
      </button>
    </div>
  );
}
