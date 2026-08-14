"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useFormState, useFormStatus } from "react-dom";
import { Ban, Eye, RotateCcw, ShieldOff, Users } from "lucide-react";
import { Avatar, Badge, DataTable, EmptyState, useToast, type Column } from "@kurx/ui";
import { moderateUserAction } from "@/lib/admin-actions";
import type { AdminUser } from "@/lib/api";

const STATUS_TONE = { active: "success", suspended: "muted", banned: "danger" } as const;

function ActionButton({ action, label, icon: Icon, tone }: { action: string; label: string; icon: typeof Ban; tone: "warn" | "danger" | "ok" }) {
  const { pending } = useFormStatus();
  const cls =
    tone === "danger" ? "border-danger/40 text-danger hover:bg-danger/10"
      : tone === "ok" ? "border-success/40 text-success hover:bg-success/10"
        : "border-border text-muted hover:bg-elevated hover:text-text";
  return (
    <button
      type="submit"
      name="action"
      value={action}
      disabled={pending}
      title={label}
      aria-label={label}
      className={`grid h-8 w-8 place-items-center rounded-md border disabled:opacity-50 ${cls}`}
    >
      <Icon size={14} />
    </button>
  );
}

function UserActions({ user, query, linkToDetail }: { user: AdminUser; query: string; linkToDetail: boolean }) {
  const [state, action] = useFormState(moderateUserAction.bind(null, user.id), null);
  const toast = useToast();
  const moderated = user.suspended || user.banned;

  useEffect(() => {
    if (state && "ok" in state) toast(`${user.name || "User"} updated.`, "success");
    else if (state && "error" in state) toast(String(state.error), "error");
    // eslint-disable-next-line react-hooks/exhaustive-deps -- fire only when the action result changes
  }, [state]);

  return (
    <form action={action} className="flex items-center justify-end gap-1.5">
      {!moderated ? (
        <input
          name="reason"
          placeholder="Reason (optional)"
          className="h-8 w-32 rounded-md border border-border-strong bg-background px-2 text-xs text-text placeholder:text-muted focus:border-accent focus:outline-none"
        />
      ) : null}
      {moderated ? (
        <ActionButton action="unban" label="Reinstate" icon={RotateCcw} tone="ok" />
      ) : (
        <>
          <ActionButton action="suspend" label="Suspend" icon={ShieldOff} tone="warn" />
          <ActionButton action="ban" label="Ban" icon={Ban} tone="danger" />
        </>
      )}
      {linkToDetail ? (
        <Link
          href={query ? `/users/${user.id}?q=${encodeURIComponent(query)}` : `/users/${user.id}`}
          aria-label="View user"
          title="View"
          className="grid h-8 w-8 place-items-center rounded-md border border-border text-muted hover:bg-elevated hover:text-text"
        >
          <Eye size={14} />
        </Link>
      ) : null}
    </form>
  );
}

/** `linkToDetail` is off when this already IS the detail page, so the row does not link to itself. */
export function UsersManager({
  users,
  query,
  linkToDetail = true
}: {
  users: AdminUser[];
  query: string;
  linkToDetail?: boolean;
}) {
  const columns: Column<AdminUser>[] = [
    {
      key: "name",
      header: "User",
      render: (u) => (
        <div className="flex items-center gap-3">
          <Avatar name={u.name || u.phone} size={32} />
          <div className="min-w-0">
            <p className="truncate font-medium text-text">{u.name || "—"}</p>
            {u.username ? <p className="truncate text-xs text-muted">@{u.username}</p> : null}
          </div>
        </div>
      )
    },
    {
      key: "phone",
      header: "Contact",
      render: (u) => (
        <div className="text-xs text-muted">
          <p>{u.phone}</p>
          {u.email ? <p className="truncate">{u.email}</p> : null}
        </div>
      )
    },
    {
      key: "status",
      header: "Status",
      render: (u) => {
        const status = u.banned ? "banned" : u.suspended ? "suspended" : "active";
        return (
          <div>
            <Badge tone={STATUS_TONE[status]}>{status}</Badge>
            {(u.suspended || u.banned) && u.moderation_reason ? (
              <p className="mt-1 max-w-[16rem] truncate text-xs text-muted" title={u.moderation_reason}>
                {u.moderation_reason}
              </p>
            ) : null}
          </div>
        );
      }
    },
    {
      key: "actions",
      header: "",
      srHeader: "Row actions",
      align: "right",
      width: "13rem",
      render: (u) => <UserActions user={u} query={query} linkToDetail={linkToDetail} />
    }
  ];

  return (
    <DataTable
      columns={columns}
      data={users}
      keyField={(u) => u.id}
      caption="Users"
      emptyState={
        <EmptyState
          icon={<Users size={22} />}
          title={query ? "No matching users" : "No suspended or banned accounts"}
          message={query ? `No users match "${query}".` : "Search above to find any user."}
        />
      }
    />
  );
}
