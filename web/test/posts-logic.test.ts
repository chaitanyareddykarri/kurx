import { describe, expect, it } from "vitest";
import { tokenizeBody } from "@/components/posts/post-body";
import { mergePostPage, applyLike } from "@/lib/posts-feed";

/// Logic that is impractical to verify by hand (D-109): body tokenization and the cursor-paging
/// merge. Rendering is left to browser verification.

describe("tokenizeBody", () => {
  it("returns a single text token when there is nothing to link", () => {
    expect(tokenizeBody("just a plain post")).toEqual([{ type: "text", value: "just a plain post" }]);
  });

  it("links a hashtag and lowercases the tag while preserving the shown text", () => {
    expect(tokenizeBody("ship it #HackDay")).toEqual([
      { type: "text", value: "ship it " },
      { type: "hashtag", value: "#HackDay", tag: "hackday" }
    ]);
  });

  it("links a mention", () => {
    expect(tokenizeBody("cc @Asha")).toEqual([
      { type: "text", value: "cc " },
      { type: "mention", value: "@Asha", username: "asha" }
    ]);
  });

  it("links every occurrence of a repeated tag, not just the first", () => {
    const tags = tokenizeBody("#kurx and again #kurx").filter((t) => t.type === "hashtag");
    expect(tags).toHaveLength(2);
  });

  it("does not treat an email address as a mention", () => {
    // The token must be preceded by start-of-string or whitespace, so "a@b" never matches.
    expect(tokenizeBody("mail me at asha@kurx.in")).toEqual([
      { type: "text", value: "mail me at asha@kurx.in" }
    ]);
  });

  it("does not swallow trailing punctuation into the tag", () => {
    expect(tokenizeBody("see #finals, then go")).toEqual([
      { type: "text", value: "see " },
      { type: "hashtag", value: "#finals", tag: "finals" },
      { type: "text", value: ", then go" }
    ]);
  });

  it("links a tag at the very start of the body", () => {
    expect(tokenizeBody("#first post")[0]).toEqual({ type: "hashtag", value: "#first", tag: "first" });
  });

  it("ignores a bare # or @ with no word after it", () => {
    expect(tokenizeBody("a # b @ c")).toEqual([{ type: "text", value: "a # b @ c" }]);
  });
});

const post = (id: string) => ({ id }) as never;

describe("mergePostPage", () => {
  it("appends the next page after the current items", () => {
    const merged = mergePostPage([post("a"), post("b")], { items: [post("c")], next_cursor: "x" });
    expect(merged.items.map((p) => p.id)).toEqual(["a", "b", "c"]);
    expect(merged.nextCursor).toBe("x");
  });

  it("drops a duplicate id rather than rendering the same post twice", () => {
    // A post created between two page fetches shifts the keyset, so the same row can arrive twice.
    const merged = mergePostPage([post("a"), post("b")], { items: [post("b"), post("c")], next_cursor: null });
    expect(merged.items.map((p) => p.id)).toEqual(["a", "b", "c"]);
  });

  it("reports exhaustion when the server returns no cursor", () => {
    expect(mergePostPage([], { items: [], next_cursor: null }).nextCursor).toBeNull();
  });
});

describe("applyLike", () => {
  const feed = [
    { id: "a", liked_by_me: false, like_count: 2 },
    { id: "b", liked_by_me: true, like_count: 9 }
  ] as never[];

  it("writes the server's count onto the matching post only", () => {
    const next = applyLike(feed, "a", { liked: true, like_count: 3 });
    expect(next[0]).toMatchObject({ liked_by_me: true, like_count: 3 });
    expect(next[1]).toMatchObject({ liked_by_me: true, like_count: 9 });
  });

  it("leaves the feed untouched when the id is not present", () => {
    expect(applyLike(feed, "zzz", { liked: true, like_count: 1 })).toEqual(feed);
  });
});
