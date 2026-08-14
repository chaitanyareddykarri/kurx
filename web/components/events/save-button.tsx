"use client";

import { useState, useTransition } from "react";
import { Bookmark, BookmarkCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { toggleSaveAction } from "@/lib/social-actions";

export function SaveButton({ eventId, initialSaved }: { eventId: string; initialSaved: boolean }) {
  const [saved, setSaved] = useState(initialSaved);
  const [pending, start] = useTransition();

  return (
    <Button
      variant={saved ? "secondary" : "primary"}
      disabled={pending}
      className="w-full"
      onClick={() => start(async () => setSaved(await toggleSaveAction(eventId, saved)))}
    >
      {saved ? <BookmarkCheck size={16} /> : <Bookmark size={16} />}
      {saved ? "Saved" : "Save event"}
    </Button>
  );
}
