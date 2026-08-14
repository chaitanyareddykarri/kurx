import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const postsActions = vi.hoisted(() => ({
  togglePostLikeAction: vi.fn(),
  togglePostSavedAction: vi.fn(),
  deletePostAction: vi.fn(),
  loadFeedAction: vi.fn(),
  loadMyPostsAction: vi.fn(),
  loadSavedPostsAction: vi.fn(),
  loadUserPostsAction: vi.fn(),
  loadEventPostsAction: vi.fn(),
  loadHashtagPostsAction: vi.fn(),
}));

vi.mock("@/lib/posts-actions", () => postsActions);
vi.mock("@/lib/api", () => ({
  apiErrorMessage: (err: unknown) => (err instanceof Error ? err.message : "Something went wrong."),
}));

import { ToastProvider } from "@kurx/ui";
import { PostCard } from "@/components/posts/post-card";
import { PostFeed } from "@/components/posts/post-feed";

/**
 * Guards for Phase 20A — posts and the social feed.
 *
 * `PostCard` is the most-rendered component in the product; every defect it carries is multiplied by
 * the length of a feed.
 */

function post(over: Record<string, unknown> = {}) {
  return {
    id: "p1",
    body: "Shipped the thing.",
    created_at: new Date("2026-01-01T00:00:00Z").toISOString(),
    edited_at: null,
    visibility: "public",
    author: { id: "u1", name: "Asha Rao", username: "asha", avatar_key: null, is_verified: false },
    media: [],
    poll: null,
    event: null,
    shared_post: null,
    like_count: 42,
    comment_count: 7,
    share_count: 3,
    liked_by_me: false,
    saved_by_me: false,
    ...over,
  } as never;
}

const noop = async () => {};

beforeEach(() => vi.clearAllMocks());

describe("PostCard — counts must be announced, not just drawn", () => {
  it("puts the count in the control's accessible name", () => {
    render(<PostCard post={post()} onLike={noop} onSave={noop} />);
    // `aria-label` replaces content, so `aria-label="Like"` around a visible "42" left every count
    // in the feed silent.
    expect(screen.getByRole("button", { name: "Like, 42" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Comments, 7" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Share, 3" })).toBeInTheDocument();
  });

  it("omits a zero count rather than announcing it", () => {
    render(<PostCard post={post({ like_count: 0 })} onLike={noop} onSave={noop} />);
    expect(screen.getByRole("button", { name: "Like" })).toBeInTheDocument();
  });
});

describe("PostCard — a toggle must expose its state", () => {
  it("marks like and save with aria-pressed", () => {
    render(<PostCard post={post({ liked_by_me: true, saved_by_me: true })} onLike={noop} onSave={noop} />);
    // The state was carried by fill and hue alone, which no screen reader perceives.
    expect(screen.getByRole("button", { name: /Unlike/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Unsave" })).toHaveAttribute("aria-pressed", "true");
  });

  it("does not claim a pressed state on a plain action", () => {
    render(<PostCard post={post()} onLike={noop} onSave={noop} onShare={() => {}} />);
    expect(screen.getByRole("button", { name: /^Share/ })).not.toHaveAttribute("aria-pressed");
  });
});

describe("PostCard — no control may advertise itself and go nowhere", () => {
  it("renders an author with no username as text, not as a link to #", () => {
    render(
      <PostCard
        post={post({ author: { id: "u1", name: "No Handle", username: null, avatar_key: null, is_verified: false } })}
        onLike={noop}
        onSave={noop}
      />
    );
    expect(screen.queryByRole("link", { name: "No Handle" })).not.toBeInTheDocument();
    expect(screen.getByText("No Handle")).toBeInTheDocument();
  });

  it("links an author who has one", () => {
    render(<PostCard post={post()} onLike={noop} onSave={noop} />);
    // The avatar and the name are both links to the same profile — the card's existing design.
    const links = screen.getAllByRole("link", { name: "Asha Rao" });
    expect(links).toHaveLength(2);
    for (const link of links) expect(link).toHaveAttribute("href", "/u/asha");
  });

  it("renders verification as a badge, not a bare check mark", () => {
    render(<PostCard post={post({ author: { id: "u1", name: "Asha Rao", username: "asha", avatar_key: null, is_verified: true } })} onLike={noop} onSave={noop} />);
    expect(screen.getByText("Verified")).toBeInTheDocument();
    expect(screen.queryByText("✓")).not.toBeInTheDocument();
  });
});

describe("PostFeed — an action that fails must say so", () => {
  it("reports a failed like instead of just flipping the heart back", async () => {
    postsActions.togglePostLikeAction.mockRejectedValue(new Error("Could not like that"));
    render(
      <ToastProvider>
        <PostFeed source={{ kind: "feed" }} initial={{ items: [post()], next_cursor: null } as never} />
      </ToastProvider>
    );

    await userEvent.click(screen.getByRole("button", { name: "Like, 42" }));

    await screen.findByText(/Could not like that/);
    // And the count is back where it started.
    expect(screen.getByRole("button", { name: "Like, 42" })).toBeInTheDocument();
  });

  it("announces how many posts a Load more actually added", async () => {
    postsActions.loadFeedAction.mockResolvedValue({ items: [post({ id: "p2" })], next_cursor: null });
    render(
      <ToastProvider>
        <PostFeed source={{ kind: "feed" }} initial={{ items: [post()], next_cursor: "c1" } as never} />
      </ToastProvider>
    );

    await userEvent.click(screen.getByRole("button", { name: /Load more/ }));
    // The posts arrived below the button and were never announced, so the control read as inert.
    expect(await screen.findByText("1 more post loaded.")).toBeInTheDocument();
  });

  it("reports a failed page load", async () => {
    postsActions.loadFeedAction.mockRejectedValue(new Error("Feed is unavailable"));
    render(
      <ToastProvider>
        <PostFeed source={{ kind: "feed" }} initial={{ items: [post()], next_cursor: "c1" } as never} />
      </ToastProvider>
    );

    await userEvent.click(screen.getByRole("button", { name: /Load more/ }));
    await screen.findByText(/Feed is unavailable/);
  });
});
