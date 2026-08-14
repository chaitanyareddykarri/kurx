"use client";

import { useState } from "react";
import { Flag, MoreHorizontal, Pencil, Trash2 } from "lucide-react";
import { EditPostDialog } from "@/components/posts/edit-post-dialog";
import { ReportDialog } from "@/components/posts/report-dialog";
import type { Post } from "@/lib/posts-api";

/// Author actions and reader actions in one menu, filtered by what the server says this viewer may
/// do (`can_edit`/`can_delete`) — never by comparing ids on the client.
export function PostActionsMenu({
  post,
  onDelete,
  onUpdated
}: {
  post: Post;
  onDelete?: (post: Post) => Promise<void> | void;
  onUpdated?: (post: Post) => void;
}) {
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  const [reporting, setReporting] = useState(false);

  return (
    <>
      <div className="relative">
        <button
          type="button"
          aria-label="Post options"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
          className="rounded-md px-2.5 py-1.5 text-xs text-muted hover:bg-elevated hover:text-text"
        >
          <MoreHorizontal size={16} />
        </button>

        {open ? (
          <>
            {/* Click-away layer, so the menu closes on any outside click without a document listener. */}
            <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} aria-hidden />
            <div className="absolute right-0 z-20 mt-1 w-40 rounded-md border border-border bg-surface p-1 shadow-lg">
              {post.can_edit ? (
                <MenuItem
                  icon={<Pencil size={14} />}
                  label="Edit post"
                  onClick={() => {
                    setOpen(false);
                    setEditing(true);
                  }}
                />
              ) : null}
              {post.can_delete && onDelete ? (
                <MenuItem
                  icon={<Trash2 size={14} />}
                  label="Delete post"
                  danger
                  onClick={() => {
                    setOpen(false);
                    void onDelete(post);
                  }}
                />
              ) : null}
              {/* Reporting your own post is not offered — there is nobody to escalate to. */}
              {!post.can_delete ? (
                <MenuItem
                  icon={<Flag size={14} />}
                  label="Report post"
                  onClick={() => {
                    setOpen(false);
                    setReporting(true);
                  }}
                />
              ) : null}
            </div>
          </>
        ) : null}
      </div>

      {editing ? (
        <EditPostDialog
          post={post}
          onClose={() => setEditing(false)}
          onSaved={(updated) => {
            setEditing(false);
            onUpdated?.(updated);
          }}
        />
      ) : null}

      {reporting ? (
        <ReportDialog entityType="post" entityId={post.id} onClose={() => setReporting(false)} />
      ) : null}
    </>
  );
}

function MenuItem({
  icon,
  label,
  onClick,
  danger = false
}: {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
  danger?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm hover:bg-elevated ${
        danger ? "text-danger" : "text-text"
      }`}
    >
      {icon} {label}
    </button>
  );
}
