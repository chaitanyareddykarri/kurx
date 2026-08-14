"use client";

import { useFormState, useFormStatus } from "react-dom";
import { generateCertificatesAction } from "@/lib/certificate-actions";
import { Button } from "@kurx/ui";

function SubmitButton() {
  const { pending } = useFormStatus();
  return <Button type="submit" disabled={pending}>{pending ? "Generating…" : "Generate certificates"}</Button>;
}

export function GenerateCertificatesForm({ eventId }: { eventId: string }) {
  const [state, formAction] = useFormState(generateCertificatesAction.bind(null, eventId), null);

  return (
    <form action={formAction} className="flex flex-wrap items-center gap-3">
      <SubmitButton />
      {state && "generated" in state ? <span className="text-sm text-success">{state.generated} certificate(s) generated.</span> : null}
      {state && "error" in state ? <span className="text-sm text-danger">{String(state.error)}</span> : null}
    </form>
  );
}
