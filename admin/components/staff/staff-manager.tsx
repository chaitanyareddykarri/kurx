"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Users, UserPlus, X } from "lucide-react";
import { Avatar, Badge, Button, DataTable, EmptyState, Field, Input, PhoneField, Select, Spinner, useToast, type Column } from "@kurx/ui";
import { PLATFORM_ROLES, ROLE_LABELS, type PlatformRole } from "@/lib/roles";
import type { StaffMember } from "@/lib/api";
import { grantStaffAction, revokeStaffAction } from "@/lib/staff-actions";

export function StaffManager({
  initialStaff,
  currentUserId
}: {
  initialStaff: StaffMember[];
  currentUserId: string;
}) {
  const router = useRouter();
  const toast = useToast();
  const [pending, startTransition] = useTransition();
  const [phone, setPhone] = useState("");
  const [phoneValid, setPhoneValid] = useState(false);
  const [role, setRole] = useState<PlatformRole>("Support");
  const [error, setError] = useState<string | null>(null);

  function run(fn: () => Promise<{ ok: true } | { ok: false; error: string }>, okMessage: string, onOk?: () => void) {
    setError(null);
    startTransition(async () => {
      const res = await fn();
      if (res.ok) {
        toast(okMessage, "success");
        onOk?.();
        router.refresh();
      } else {
        setError(res.error);
        toast(res.error, "error");
      }
    });
  }

  const columns: Column<StaffMember>[] = [
    {
      key: "name",
      header: "Staff member",
      render: (s) => (
        <div className="flex items-center gap-3">
          <Avatar name={s.name || s.phone} size={32} />
          <div className="min-w-0">
            <p className="flex items-center gap-1.5 truncate font-medium text-text">
              {s.name || "—"}
              {s.user_id === currentUserId ? <Badge tone="neutral">you</Badge> : null}
            </p>
            {s.username ? <p className="truncate text-xs text-muted">@{s.username}</p> : null}
          </div>
        </div>
      )
    },
    { key: "phone", header: "Phone", render: (s) => <span className="text-muted">{s.phone}</span> },
    {
      key: "roles",
      header: "Roles",
      render: (s) => (
        <div className="flex flex-wrap gap-1.5">
          {s.roles.map((r) => (
            <span
              key={r}
              className="inline-flex items-center gap-1 rounded-full border border-border bg-background px-2 py-0.5 text-xs text-text"
            >
              {ROLE_LABELS[r as PlatformRole] ?? r}
              <button
                type="button"
                disabled={pending}
                onClick={() => run(() => revokeStaffAction(s.user_id, r), `${ROLE_LABELS[r as PlatformRole] ?? r} revoked.`)}
                aria-label={`Revoke ${r}`}
                title={`Revoke ${r}`}
                className="text-muted hover:text-danger disabled:opacity-50"
              >
                <X size={12} />
              </button>
            </span>
          ))}
        </div>
      )
    }
  ];

  return (
    <div className="space-y-6">
      {/* Add / grant */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          run(() => grantStaffAction(phone, role), "Role granted.", () => setPhone(""));
        }}
        className="grid gap-3 rounded-lg border border-border bg-surface p-4 md:grid-cols-[14rem_16rem_auto] md:items-end"
      >
        {/* Country picker, not a bare tel box: the value goes straight to NormalizePhone, which reads
            digits with no '+' in the legacy region — so a staff member's overseas number would be
            granted the role against an unrelated Indian number. */}
        <PhoneField
          id="staff-phone"
          label="Phone number"
          value={phone}
          onChange={(e164, valid) => { setPhone(e164); setPhoneValid(valid); }}
        />
        <Field label="Role" htmlFor="staff-role">
          <Select id="staff-role" value={role} onChange={(e) => setRole(e.target.value as PlatformRole)}>
            {PLATFORM_ROLES.map((r) => (
              <option key={r} value={r}>
                {ROLE_LABELS[r]}
              </option>
            ))}
          </Select>
        </Field>
        <Button type="submit" disabled={pending || !phoneValid} className="sm:mb-0">
          {pending ? <Spinner size={16} className="text-white" /> : <UserPlus size={16} />}
          Grant role
        </Button>
      </form>

      {error ? (
        <p role="alert" className="text-sm text-danger">
          {error}
        </p>
      ) : null}

      <DataTable
        columns={columns}
        data={initialStaff}
        keyField={(s) => s.user_id}
        caption="Platform staff"
        emptyState={
          <EmptyState
            icon={<Users size={22} />}
            title="No staff yet"
            message="Grant a role above to add your first platform staff member."
          />
        }
      />
    </div>
  );
}
