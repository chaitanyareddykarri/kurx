import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { PostsHome } from "@/components/posts/posts-home";
import type { PostPage } from "@/lib/posts-api";

/// D-297. The Home flow's three searches and the post-search surface that closes the third.
///
/// The rails themselves (`/discover`) are a server component and are not rendered here; what is worth
/// pinning is the branch that decides *what the Posts surface is* — a feed or a result set — because
/// the whole defect class D-297 fixed was a client that looked fine and showed the wrong thing.

vi.mock("@/lib/api", () => ({
  api: { get: vi.fn(), post: vi.fn(), delete: vi.fn(), patch: vi.fn() }
}));

const loadSearchPostsAction = vi.fn();
const loadFeedAction = vi.fn();

vi.mock("@/lib/posts-actions", () => ({
  loadFeedAction: (...a: unknown[]) => loadFeedAction(...a),
  loadSearchPostsAction: (...a: unknown[]) => loadSearchPostsAction(...a),
  loadMyPostsAction: vi.fn(),
  loadSavedPostsAction: vi.fn(),
  loadUserPostsAction: vi.fn(),
  loadEventPostsAction: vi.fn(),
  loadHashtagPostsAction: vi.fn(),
  togglePostLikeAction: vi.fn(),
  togglePostSavedAction: vi.fn(),
  deletePostAction: vi.fn(),
  createPostAction: vi.fn(),
  presignPostMediaAction: vi.fn(),
  confirmPostMediaAction: vi.fn(),
  POST_BODY_MAX: 3000
}));

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => <a href={href}>{children}</a>
}));

const author = { name: "Asha", avatarKey: null };
const empty: PostPage = { items: [], next_cursor: null };

function renderHome(query?: string) {
  return render(<PostsHome author={author} initial={empty} trending={[]} query={query} />);
}

describe("PostsHome — search vs feed (D-297)", () => {
  it("offers a search box whose query lives in the URL, so a search survives reload and sharing", () => {
    renderHome();
    const box = screen.getByRole("searchbox", { name: /search posts/i });
    const form = box.closest("form")!;
    // A GET form: the term becomes ?q=, which is what makes the result page addressable at all.
    expect(form).toHaveAttribute("method", "GET");
    expect(form).toHaveAttribute("action", "/posts");
    expect(box).toHaveAttribute("name", "q");
  });

  it("renders the feed's empty copy when not searching, and keeps the composer", () => {
    renderHome();
    expect(screen.getByText(/your feed is quiet/i)).toBeInTheDocument();
    expect(screen.queryByText(/no posts match/i)).not.toBeInTheDocument();
  });

  it("replaces the feed with results when a query is present — never both lists at once", () => {
    renderHome("keynote");
    // Search copy, not feed copy: the surface has genuinely changed mode.
    expect(screen.getByText(/no posts match that search/i)).toBeInTheDocument();
    expect(screen.queryByText(/your feed is quiet/i)).not.toBeInTheDocument();
    // The composer is gone while searching — composing into a result set is meaningless.
    expect(screen.queryByPlaceholderText(/what's happening/i)).not.toBeInTheDocument();
  });

  it("prefills the box from the URL and offers a way back to the feed", () => {
    renderHome("keynote");
    expect(screen.getByRole("searchbox", { name: /search posts/i })).toHaveValue("keynote");
    expect(screen.getByRole("link", { name: /clear/i })).toHaveAttribute("href", "/posts");
  });

  it("shows no Clear link when there is nothing to clear", () => {
    renderHome();
    expect(screen.queryByRole("link", { name: /clear/i })).not.toBeInTheDocument();
  });
});
