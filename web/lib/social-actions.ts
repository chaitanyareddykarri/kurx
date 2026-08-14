"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import {
  saveEvent, unsaveEvent, followOrg, unfollowOrg,
  upsertEventReview, deleteMyReview, apiErrorMessage,
  requestAlly, acceptAllyRequest, declineAllyRequest, revokeAlly,
  searchPublicUsers, getAllyStatusBatch, getAllyMutualDetail, getAllySuggestions,
  type AllyConnection, type AllyRelationStatus, type PublicUserSearchResult,
  type MutualDetail, type AllySuggestion,
} from "@/lib/api";

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

// Toggle helpers return the new state so the client button can flip optimistically
// without a full page revalidate.
export async function toggleSaveAction(eventId: string, currentlySaved: boolean): Promise<boolean> {
  const session = await requireSession();
  if (currentlySaved) {
    await unsaveEvent(session.accessToken, eventId);
    return false;
  }
  await saveEvent(session.accessToken, eventId);
  return true;
}

export async function toggleFollowAction(orgId: string, currentlyFollowing: boolean): Promise<boolean> {
  const session = await requireSession();
  if (currentlyFollowing) {
    await unfollowOrg(session.accessToken, orgId);
    return false;
  }
  await followOrg(session.accessToken, orgId);
  return true;
}

// Allies (D-201): a request/accept/decline/revoke state machine, not a toggle — the server always
// returns the connection's real resulting status rather than an assumed boolean flip.
export async function requestAllyAction(targetUserId: string): Promise<AllyConnection> {
  const session = await requireSession();
  return requestAlly(session.accessToken, targetUserId);
}

export async function respondAllyRequestAction(connectionId: string, accept: boolean): Promise<AllyConnection> {
  const session = await requireSession();
  return accept ? acceptAllyRequest(session.accessToken, connectionId) : declineAllyRequest(session.accessToken, connectionId);
}

export async function revokeAllyAction(connectionId: string): Promise<void> {
  const session = await requireSession();
  await revokeAlly(session.accessToken, connectionId);
}

/** People search (D-20x): a public search plus one batch call for every result's ally status,
 * so the result grid never fires a request per card. */
export async function searchPeopleAction(
  query: string
): Promise<{ user: PublicUserSearchResult; status: AllyRelationStatus }[]> {
  const session = await requireSession();
  const results = await searchPublicUsers(query);
  const others = results.filter((r) => r.id !== session.me.id).map((r) => r.id);
  const statuses = await getAllyStatusBatch(session.accessToken, others);
  return results.map((user) => ({ user, status: statuses[user.id] ?? "none" }));
}

export async function getMutualDetailAction(otherUserId: string): Promise<MutualDetail> {
  const session = await requireSession();
  return getAllyMutualDetail(session.accessToken, otherUserId);
}

export async function getMySuggestionsAction(): Promise<AllySuggestion[]> {
  const session = await requireSession();
  return getAllySuggestions(session.accessToken);
}

export async function submitReviewAction(eventId: string, slug: string, _prev: unknown, formData: FormData) {
  const session = await requireSession();
  const rating = Number(formData.get("rating"));
  if (!Number.isInteger(rating) || rating < 1 || rating > 5) return { error: "Pick a rating from 1 to 5." };
  try {
    await upsertEventReview(session.accessToken, eventId, {
      rating,
      title: str(formData, "title"),
      body: str(formData, "body"),
      isAnonymous: formData.get("anonymous") === "on",
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath(`/e/${slug}`);
  return { ok: true };
}

export async function deleteReviewAction(eventId: string, slug: string) {
  const session = await requireSession();
  await deleteMyReview(session.accessToken, eventId);
  revalidatePath(`/e/${slug}`);
}
