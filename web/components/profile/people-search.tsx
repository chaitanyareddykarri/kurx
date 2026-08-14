"use client";

import { useState, useTransition } from "react";
import { SearchBar, Spinner, UserCard } from "@kurx/ui";
import type { AllyRelationStatus, PublicUserSearchResult } from "@/lib/api";
import { apiErrorMessage } from "@/lib/api";
import { toAllyRelation } from "@/lib/ally-status";
import { AllyConnectButton } from "@/components/profile/ally-connect-button";
import { searchPeopleAction } from "@/lib/social-actions";

const MIN_QUERY = 2;
const RESULTS_ID = "people-search-results";

type Hit = { user: PublicUserSearchResult; status: AllyRelationStatus };

/**
 * Finding people to connect with.
 *
 * Rebuilt onto `SearchBar`, the primitive Phase 12 hardened for exactly this. What the hand-rolled
 * version had instead, each of which a keyboard or screen-reader user hit first:
 *
 * * The input carried **no label** — only a placeholder, which is not an accessible name and
 *   disappears the moment anything is typed. It announced as an unlabelled edit field.
 * * Results replaced the region **silently**: no live region, so the search appeared to do nothing.
 * * A query under two characters **did nothing at all** — no request, no message. The button looked
 *   broken, and there was no way to find out why.
 * * `border-border` is the decorative token (1.30:1) on a control boundary WCAG 1.4.11 requires to
 *   clear 3:1, and both input and button stood 40px against the 44px touch floor.
 * * The action was unguarded, so a failed search rejected into the transition and the region simply
 *   stayed as it was.
 */
export function PeopleSearch() {
  const [query, setQuery] = useState("");
  const [searched, setSearched] = useState("");
  const [results, setResults] = useState<Hit[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, start] = useTransition();

  function onQuery(next: string) {
    setQuery(next);
    const trimmed = next.trim();
    // Clearing the box clears the results with it, rather than leaving a stale list under an empty
    // query that no longer describes it.
    if (trimmed.length < MIN_QUERY) {
      setResults(null);
      setSearched("");
      setError(null);
      return;
    }
    start(async () => {
      try {
        setResults(await searchPeopleAction(trimmed));
        setSearched(trimmed);
        setError(null);
      } catch (err) {
        setError(apiErrorMessage(err));
        setResults(null);
      }
    });
  }

  const tooShort = query.trim().length > 0 && query.trim().length < MIN_QUERY;

  return (
    <div>
      <SearchBar
        value={query}
        onChange={onQuery}
        aria-label="Search people by name or username"
        placeholder="Search by name or @username"
        resultCount={results?.length}
        controls={RESULTS_ID}
      />

      <p className="mt-1.5 text-caption text-muted">
        {tooShort ? `Keep typing — searching starts at ${MIN_QUERY} characters.` : `Name or @username.`}
      </p>

      {/* A failed search is an error, not an empty result: saying "no profiles match" when the
          request never completed would be a claim about the people, not about the outage. */}
      {error && (
        <p role="alert" className="mt-3 text-sm text-danger">
          {error} Try searching again.
        </p>
      )}

      <div id={RESULTS_ID}>
        {pending && (
          <p className="mt-4 flex items-center gap-2 text-sm text-muted">
            <Spinner size={16} label="Searching people" />
            <span aria-hidden>Searching…</span>
          </p>
        )}

        {!pending && results && !error && (
          results.length === 0 ? (
            <p className="mt-4 text-sm text-muted">No public profiles match &ldquo;{searched}&rdquo;.</p>
          ) : (
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              {results.map(({ user, status }) => (
                <UserCard
                  key={user.id}
                  name={user.name}
                  username={user.username}
                  avatarSrc={user.avatar_key}
                  subtitle={user.headline ?? undefined}
                  href={`/u/${user.username}`}
                  action={
                    <AllyConnectButton
                      targetUserId={user.id}
                      targetName={user.name}
                      initialRelation={toAllyRelation(status)}
                      initialConnectionId={null}
                    />
                  }
                />
              ))}
            </div>
          )
        )}
      </div>
    </div>
  );
}
