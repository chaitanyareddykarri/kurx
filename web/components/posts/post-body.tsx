import Link from "next/link";
import { Fragment } from "react";

/// Splits a post/comment body into plain text, #hashtags and @mentions.
///
/// The server extracts hashtags and mentions authoritatively at create time (they are columns, not
/// derived at read time) — this is display only. It deliberately re-scans the body rather than
/// consuming the `hashtags`/`mentions` arrays, because those carry no positions and the same tag can
/// appear several times; matching here is what keeps every occurrence linked.
const TOKEN = /(^|[\s(])([#@][A-Za-z0-9_]{1,30})(?=$|[\s).,!?:;])/g;

export type BodyToken =
  | { type: "text"; value: string }
  | { type: "hashtag"; value: string; tag: string }
  | { type: "mention"; value: string; username: string };

export function tokenizeBody(body: string): BodyToken[] {
  const tokens: BodyToken[] = [];
  let last = 0;

  for (const match of body.matchAll(TOKEN)) {
    const [, lead, token] = match;
    // `match.index` points at the leading whitespace, which belongs to the preceding text run.
    const start = (match.index ?? 0) + lead.length;
    if (start > last) tokens.push({ type: "text", value: body.slice(last, start) });

    const rest = token.slice(1);
    tokens.push(
      token[0] === "#"
        ? { type: "hashtag", value: token, tag: rest.toLowerCase() }
        : { type: "mention", value: token, username: rest.toLowerCase() }
    );
    last = start + token.length;
  }

  if (last < body.length) tokens.push({ type: "text", value: body.slice(last) });
  return tokens;
}

export function PostBody({ body, className }: { body: string; className?: string }) {
  if (!body) return null;
  return (
    <p className={className ?? "whitespace-pre-wrap break-words text-sm text-text"}>
      {tokenizeBody(body).map((t, i) => {
        if (t.type === "text") return <Fragment key={i}>{t.value}</Fragment>;
        const href = t.type === "hashtag" ? `/posts/tag/${t.tag}` : `/u/${t.username}`;
        return (
          <Link key={i} href={href} className="font-medium text-accent hover:underline">
            {t.value}
          </Link>
        );
      })}
    </p>
  );
}
