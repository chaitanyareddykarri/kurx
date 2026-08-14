"use client";

import { markReadAction, markAllReadAction } from "@/lib/notification-actions";
import { Button } from "@/components/ui/button";

export function MarkReadButton({ id }: { id: string }) {
  return (
    <form action={markReadAction.bind(null, id)}>
      <Button type="submit" variant="secondary" className="shrink-0">Mark read</Button>
    </form>
  );
}

export function MarkAllReadButton() {
  return (
    <form action={markAllReadAction}>
      <Button type="submit" variant="secondary">Mark all read</Button>
    </form>
  );
}
