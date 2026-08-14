# Event Chat — Canonical Architecture

The reference for Kurx Event Chat across backend, Flutter and web. It describes **shipped**
behaviour as of **2026-08-08 (D-104 … D-308)**.

> ⚠️ This header read "as of 2026-07-19 (D-104 … D-124)" until 2026-08-08 — three weeks and roughly a
> dozen decisions behind, while still calling itself canonical and being linked from seven places. The
> membership table and the moderation model were both describing the pre-D-300 world, in which an accepted
> staff assignment made someone a Host. Sections corrected: **6a Membership**, **9 Moderation**. The
> messaging capabilities added by D-293/D-295/D-296 (edit, delete-for-me, reactions, forward, link
> previews, shared media, search, receipts, expiring pins) are documented per-surface in the companion
> files rather than restated here. Where a decision is load-bearing, the `D-NNN` that made
it is cited; the decision log is append-only and remains the history, this document is the current
state.

**This is the only chat document (D-309).** Schema, REST contract, realtime, security, backend detail,
web and Flutter all live below as sections of this file.

D-104 split chat documentation into seven per-surface files, each owning one fact. The intent was right;
the outcome was seven files drifting independently, and on 2026-08-08 a single audit found every one of
them materially wrong at the same time: a deleted method still named as a caller, two tables missing
entirely with a correction note that had inverted, five decisions' worth of endpoints absent, shipped
features listed as "out of scope", 4 of 13 realtime events undocumented, and this page frozen three weeks
behind while calling itself canonical.

One document can still be wrong. It can only be wrong in **one place**, and the next reader notices.

| Part | What it covers |
|---|---|
| §1 – §16 | Cross-platform architecture: rooms, membership, moderation, presence, security, lifecycle |
| Database schema | Every `chat_*` table, from the EF model snapshot |
| REST contract | All 39 endpoints, error mapping, the frozen Phase-1 changes |
| Realtime — SignalR | The versioned envelope and every event the server emits |
| Security model — detail | Authorization, rate limits, uploads, the malware scanner |
| Backend architecture — detail | Service internals and the lifecycle hooks |
| Web client · Flutter client | Per-surface behaviour, offline rules, upload pipelines |

---

## 1. System overview

One event is one chat room. For an *event* room, users never create it: it is created when the event
is published, and **event membership is chat membership**. Most of this document follows from that
single rule — there is no group management, no invite flow, and no room discovery beyond "events I am
part of".

> **Direct messages (D-264) extended this, without changing it.** `ChatRoom.EventId` is now nullable
> and `ChatRoomKind` has a `Direct` member, so a 1:1 conversation is *the same `ChatRoom`* — the same
> messages, attachments, presence, read pointers, hub and offline outbox, with no parallel
> implementation. The per-event uniqueness index is scoped to `"EventId" IS NOT NULL`, so everything
> written below about event rooms means exactly what it meant. What is genuinely new is confined to
> §6b. This paragraph replaces the original claim that "users never create direct conversations",
> which was true until D-264.

```
 Flutter (Riverpod)          Web (Next.js / React)
        │                             │
        │  REST (history, send, moderation, attachments)
        │  SignalR /hubs/chat (live updates, presence)
        ▼                             ▼
 ┌─────────────────────────────────────────────┐
 │ Kurx.Api      ChatEndpoints · ChatHub       │
 ├─────────────────────────────────────────────┤
 │ Kurx.Infrastructure   ChatService           │
 │   IStorage · IFileScanner · IPresenceService│
 │   INotificationService · IReportService     │
 ├─────────────────────────────────────────────┤
 │ Kurx.Application      interfaces only       │
 │ Kurx.Domain           entities, no framework│
 └─────────────────────────────────────────────┘
        │                │              │
   PostgreSQL        Redis (opt)     FCM (push)
```

**Realtime is an optimisation, never the source of truth.** Every client degrades to REST plus
cursor sync when the socket is unavailable, and a lost connection is never surfaced as "chat is
broken". This is why no feature is implemented as a local mutation driven by a socket event.

---

## 2. Backend architecture

`ChatService` (`Kurx.Infrastructure/Chat/ChatService.cs`) owns all chat behaviour. `Kurx.Api` holds
no business logic and never touches EF for it.

| Concern | Where |
|---|---|
| REST surface | `Kurx.Api/Endpoints/ChatEndpoints.cs` |
| Realtime surface | `Kurx.Api/Hubs/ChatHub.cs` — `JoinRoom`, `LeaveRoom`, `SendMessage`, `Typing`, `Heartbeat` |
| Behaviour | `Kurx.Infrastructure/Chat/ChatService.cs` |
| Presence store | `Kurx.Infrastructure/Presence/RedisPresenceService.cs`, `PresenceDisabledService.cs` |
| Upload policy | `Kurx.Infrastructure/Chat/AttachmentPolicy.cs` |
| Lifecycle jobs | `LockExpiredChatRoomsJob`, attachment orphan sweep (Hangfire) |

**Contract invariants (D-104), frozen before any client was built:**

- **UUIDv7 ids.** k-sortable, so id order is time order — the property read receipts and cursors both
  depend on.
- **`ClientMessageId`** is the idempotency key per room. A retry with the same id can never create a
  second message, which is what makes optimistic send safe.
- **Opaque keyset cursors** (`before` / `after`) over `(CreatedAt, Id)`, translated by Npgsql to a
  row-wise `(a,b) > (c,d)` comparison. Never `CreatedAt` alone — rows predating D-104 hold v4 ids.
- **`LastReadMessageId`** is a room-level, monotonic read pointer. `LastReadAt` is analytics only.
- **Server-computed `capabilities`.** Clients render from these flags and never re-derive permission
  from role plus policy. A future chat mode is a server change with no app release.
- **Versioned envelope** `{ v, type, roomId, id, payload }` on a single `chat` method. Unknown types
  and unknown fields are ignored by contract, so new events ship server-first.

**Room lifecycle.** See §6a — chat state is entirely derived from event state (D-122).

---

## 3. Flutter architecture

`mobile/lib/features/social/` — extends the existing feature, with no parallel chat module.

```
data/datasources/  chat_remote_datasource.dart · chat_hub_client.dart · chat_cache.dart
data/models/       chat_mappers.dart
domain/            chat_message.dart · chat_room.dart · chat_attachment.dart · chat_repository.dart
presentation/      chat_providers.dart  ← all state
                   pages/chat_room_page.dart · pages/my_chats_page.dart
                   widgets/chat_message_bubble.dart · chat_attachment_view.dart · chat_presence.dart
```

`ChatRoomController` (Riverpod `StateNotifier`, keyed by **eventId** — the room is addressable only
as `GET /v1/events/{eventId}/chat`) holds one `ChatRoomState`: messages, room, uploads, and presence.
No separate presence controller and no widget-local presence maps; widgets read derived getters
(`typistsExcept`, `isOnline`, `highestReadByOthers`).

Offline is real here: Hive caches history and an outbox, so a cold start offline renders a populated
room rather than a spinner (D-108).

**Lifecycle rule learned the hard way (D-118):** the controller is `autoDispose`, so every
continuation past an `await` re-checks `mounted`. Without it, leaving a room mid-load throws — it
surfaced as cross-test pollution but was a production crash path.

---

## 4. Web architecture

`web/` — Next.js App Router, desktop-first.

```
lib/chat-api.ts        zod schemas + typed client
lib/chat-actions.ts    server actions (the token never reaches the browser)
lib/chat-merge.ts      ordering, optimistic reconciliation, cursors
lib/chat-presence.ts   presence reducers + derived selectors
lib/chat-upload.ts     upload rules
lib/use-chat-room.ts   ← all room state
lib/use-chat-hub.ts    SignalR client
components/chat/       chat-room-view · message-bubble · presence · attachment-view · room-list · message-actions
```

There is **no chat Zustand store** — `store/ui.ts` is the app's only Zustand store, and chat state
lives in `useChatRoom` (D-119). `useChatRoom` *is* the centralized state; presence was added to it
rather than beside it.

**Render discipline.** Presence updates are frequent, so: the reducers return the *same object* when
nothing changed (duplicate event, stale receipt, empty sweep), and `MessageBubble` is memoised and
receives presence as three primitives (`senderOnline`, `presenceKnown`, `read`). A typing event
repaints the banner and no bubbles; a read-pointer move repaints two. This is the React equivalent of
Flutter's `select`.

Web has **no offline cache** — a deliberate difference from Flutter, not an oversight.

---

## 5. SignalR event model

One method, `chat`, carrying the versioned envelope. Server → client:

| Event | Payload | Notes |
|---|---|---|
| `MessageReceived` | the message | Clients ingest directly; a bodyless envelope triggers a `?after=` reconcile |
| `MessageDeleted` · `MessagePinned` · `RoomUpdated` · `MemberMuted` · `MemberBanned` | ids / state | **Room-level**: clients re-read the server rather than mutating locally, so capabilities stay server-driven |
| `PresenceChanged` | `{ userId, online }` | Emitted only on the transition — a second tab does not re-announce |
| `TypingChanged` | `{ userId, userName, isTyping, ttlSeconds }` | Never persisted, never pushed |
| `ReadReceiptChanged` | `{ userId, lastReadMessageId }` | Emitted only when the persisted pointer actually advances |

Client → server: `JoinRoom`, `LeaveRoom`, `SendMessage`, `Typing`, `Heartbeat`.

**Unknown event types are ignored on both clients.** Not refetched — an unrecognised future event
type must not become a request storm on an older build (D-120).

**Membership is re-checked on every `JoinRoom`** (D-017), including the re-join that follows an
automatic reconnect: `withAutomaticReconnect` restores the socket but not group membership.

---

## 6. Upload lifecycle

Presign → PUT → confirm → send (D-110). One pipeline, through `IStorage`; there is no second upload
service and no chat-specific storage abstraction.

1. **Presign** — server issues a signed, expiring URL (15 min) bound to key, size and content type.
   The key is `chat/{roomId}/{guid:N}/{safeName}` so the file keeps a human name.
2. **PUT** — the client uploads directly to the returned URL with a dedicated client, so
   `Authorization` headers, cookies and interceptors never reach object storage.
3. **Confirm** — the server does **all** authoritative validation: ownership, key, size, declared vs
   actual content type, extension agreement, magic bytes, image decode (ImageSharp), and
   `IFileScanner`. A client-claimed content type is never trusted.
4. **Send** — the message references confirmed attachment ids. A message never becomes permanent
   referencing an unconfirmed attachment.

Downloads are freshly signed per use and never cached; membership is re-checked on every mint.
Objects are served `application/octet-stream` with no `Content-Disposition` — closing a stored-XSS
vector, which is why display names come from the server-validated `fileName`.

Orphans (presigned or uploaded but never confirmed) are swept with a 24h grace period.

---

## 6a. Room and membership lifecycle

Chat state is **derived from event state**. There is no independent chat lifecycle to keep in step,
which is what stops the two drifting (D-122).

### Rooms

| Event state | Chat |
|---|---|
| Draft, Pending approval, Rejected | **No room exists.** Nothing is created before approval |
| Published (approved) | Room created, hosts seeded — `Active` |
| Running | `Active` |
| Ended | `Locked` — read-only, from the first sweep after `EndsAt` |
| Ended + 7 days | `Archived` |
| Cancelled | `Locked` immediately |
| Archived (event) | `Locked` immediately, then archived by the sweep |

`ChatRoomStatus` is `Active → Locked → Archived`, one direction only. A host can lock or reopen a
room; **archiving is lifecycle-driven and cannot be set by hand** — a manually archived room would
be a state nothing ever moves out of.

### Enforced immediately, persisted by the sweep (D-123)

An event ending is not an action anybody takes — it is a timestamp passing — so there is no
transition to hook. The state is therefore **derived on every read and every gate**:

```
effective = stored, advanced by whatever the clock has already decided
            Archived        if EndsAt <= now - 7 days
            Locked          if EndsAt <= now
            stored          otherwise
```

This closes the window to zero: a room whose event ended a minute ago refuses messages now, reports
itself as `Locked`, and offers no composer — without waiting for a sweep. The stored status may still
say `Active` at that point, and that is fine, because **nothing reads the stored status directly**.

The rule itself lives in `Kurx.Domain/Entities/ChatLifecycle.cs` — a pure, framework-free function.
`ChatService`, `ChatHub` and `ChatNotificationJob` all call it; **no component computes `Locked` or
`Archived` for itself**, which is what stops the API, the hub and the background jobs disagreeing
about whether a room is closed.

**Persisted at first access, not only by the sweep (D-124).** Opening a room whose event has ended
persists the transition there and then: the system message and the `RoomUpdated` broadcast arrive with
the first reader instead of up to an hour later. Enforcement never depended on this — the derivation
already refused the send — so what this adds is the *explanation* arriving when someone is actually
looking.

Every transition, from any path, goes through one compare-and-set:

```sql
UPDATE chat_rooms SET status = @to WHERE id = @room AND status = @from
```

The database decides the winner, and only the caller that sees a row affected writes the system
message, broadcasts and notifies. That is what makes two API instances sweeping at once, a
cancellation racing the sweep, or five people opening a just-ended room simultaneously all produce
exactly one transition. Read-then-write would not do: every caller would observe `Active`, and nothing
on `ChatRoom` would make the second save fail.

Lazy persistence steps one state at a time, so a room reached long after its event still passes
through `Locked` on the way to `Archived` and both explanations are written in order.

The hourly sweep is the **recovery and durability mechanism**, not the enforcement mechanism, and not
the only persistence path. It exists for rooms nobody opens: it catches up after downtime, repairs
missed transitions, and archives quiet rooms. It uses the same rule and the same compare-and-set, so
the persisted state can never disagree with the enforced one.

Both transitions are computed from the event's `EndsAt`, not from when the previous transition
happened, so a sweep that misses a window still lands rooms in the right state instead of shifting
every subsequent deadline. The sweep is idempotent and converges Active → Archived in a single run.

The lifecycle is strictly one-way. An archived room rejects every status change, including a host
attempting to reopen it, and no path skips `Locked`.

### What each state allows

| | `Active` | `Locked` (7-day window) | `Archived` |
|---|---|---|---|
| Read history | ✅ | ✅ | ✅ |
| Open attachments | ✅ | ✅ | ✅ |
| Send messages | ✅ | ❌ `room_locked` | ❌ `room_archived` |
| Upload | ✅ | ❌ | ❌ |
| Report a message | ✅ | ✅ | ✅ |
| Moderate (delete, handle reports) | ✅ | ✅ | ✅ hosts only |
| Mute / ban (participant changes) | ✅ | ✅ | ❌ frozen |
| Membership changes | ✅ | ✅ | ❌ frozen |
| Presence and typing | ✅ | ✅ | ❌ none |
| SignalR group join | ✅ | ✅ | ✅ — see below |
| Notifications | ✅ | none to send | ❌ none |

Moderation deliberately survives archiving while participant changes do not: a report filed on the
last day of the read-only window still has to be actionable afterwards, whereas muting somebody in a
room nobody can post in achieves nothing.

**Archived rooms still join their SignalR group, deliberately.** Presence, typing, message delivery
and notifications are all off, so the connection carries almost nothing — but moderation survives
archiving (a host can still delete an offending message), and `MessageDeleted` has to reach whoever is
reading the history at that moment. Removing the join would mean a moderator's deletion stayed visible
to readers until they refreshed. The cost is one idle group membership; the benefit is that moderation
looks the same in every state.

**Nothing is ever deleted.** Archiving freezes a room; history remains subject to platform retention
policy, and authorized moderators and administrators keep access.

### Membership

Membership mirrors event participation and is never joined manually.

| Event | Chat |
|---|---|
| Publish | The event's **creator** and every Owner/Manager/Representative seat seeded as **Hosts**; a `Staff` seat and every accepted assignment join as **Members** (D-300) |
| Staff assignment accepted | Added as **Member** (D-300 — it was Host, which made every volunteer and media-team seat able to ban attendees) |
| Organization authority granted/changed/revoked **after** publish | Converged onto every published room through the outbox (D-304) |
| Registration / order confirmed (free or paid) | Added as member |
| Group join | Added as member |
| Ticket transfer | Claimant added, sender removed if they hold no other ticket |
| Refund | Removed **if no tickets remain** — a partial refund does not evict a valid attendee |
| Banned by a host | Access revoked; history retained |

**Private events** need no separate path. Accepting an invitation returns a registration link; it does
not by itself grant attendance, so membership follows the resulting order exactly as for a public
event. Wiring chat to the RSVP itself would add people who accepted and never registered.

Removal always revokes **future access only** — messages already sent stay in the history.

## 6b. Direct rooms (D-264)

A direct room is the same `ChatRoom` with `EventId = null`, `Kind = Direct`, and the participant pair
stored canonicalised as `DirectLowUserId < DirectHighUserId`. Everything in §5, §6, §7, §11 and §12
applies unchanged, because the transport and the storage are literally the same.

**One room per pair is a database constraint, not an application check.** A unique index over the
canonical pair (filtered to `DirectLowUserId IS NOT NULL`) is what makes creation idempotent: two
people tapping *Message* in the same instant both observe no room and both insert, and one loses. An
application-level "does a room exist?" test cannot prevent that, and the failure it produces — two
rooms for one conversation, each holding half the messages — is silent and unrecoverable once both
have traffic.

**Membership is the two participants, seeded at creation.** There is no join flow, so §6's table does
not apply; `ChatHub.JoinRoom` re-checks `chat_members` exactly as it does for an event room (D-017),
which is why the hub needed no change at all.

### Request state — the spam control

| State | Meaning |
|---|---|
| `Pending` | Opened by someone the recipient has not accepted as an ally. The room exists and messages are stored, but **nobody is notified** and it appears only in the recipient's Requests list. |
| `Accepted` | Either the recipient accepted, or the two were already allies at creation, in which case it starts here. |
| `Declined` | Frozen. Further sends are refused `dm_declined` in **both** directions. |

Only the recipient may answer (`not_recipient` otherwise); answering twice is `already_answered`.
Without this gate an open DM endpoint is an abuse surface from the hour it ships, and retrofitting it
later means retrofitting against users who already learned there was none.

### Blocks

A `UserBlock` in **either** direction (D-263) prevents room creation, refuses delivery, and removes the
conversation from both parties' lists. Re-checked live on every send rather than trusted from creation
— the same reasoning D-015 applies to resource roles: a block has to bite on the next message, not the
next login.

### Archiving is not the room's status

`ChatMember.ArchivedAt` is a **per-user folder**, deliberately distinct from `ChatRoomStatus.Archived`
in §6a. That status is lifecycle: derived from the event's own state, one-way, and shared by every
member. Archiving is a personal filing decision, reversible, and says nothing to the other party.
Merging them would let one person archiving a conversation freeze it for the other. Writing into an
archived conversation un-archives it for the writer.

### What direct rooms deliberately do not have

No group DMs (the pair is structural — two id columns, not a membership set), no host role, no post
policy, no lifecycle sweep (there is no event to end), and no moderation notices. Notification kind is
`dm_message`, which maps to the `messages` preference category (D-263).

## 7. Presence lifecycle

Presence is **realtime-only and never persisted** on any client — a cached "online" is a lie the
moment the app closes.

- **Authoritative store is Redis** (D-114). `IPresenceService` decides availability once:
  `RedisPresenceService` when `REDIS_CONNECTION` is set, a null-object `PresenceDisabledService`
  otherwise. **Nothing else in the codebase checks for Redis.**
- **No process-local state anywhere** — no static collection, no singleton registry, no per-server
  cache. A process-local fallback would report users offline who are connected elsewhere the moment a
  second instance ran.
- Three keys: `chat:conns:{room}:{user}` (connection ids, refcount), `chat:online:{room}` (roster in
  one round trip), `chat:conn-rooms:{connId}` (**the reverse index that makes disconnect cleanup
  possible**). TTL 120s is a crash backstop only; normal cleanup is `OnDisconnectedAsync`, and
  `Heartbeat()` (clients: every 45s) refreshes it.
- **Multi-device is refcounted**: online is announced on the *first* connection and offline only when
  the last one goes, across every device and tab.
- **Typing expires on the receiver** using the server's `ttlSeconds`, so a lost "stopped" event
  cannot strand an indicator. Outgoing typing is debounced to one `started` and one `stopped` per
  burst, and stopped on send and on unmount.
- **Read pointers only move forward.** UUIDv7 ids compare lexically as they do chronologically, so a
  stale or replayed receipt is discarded. One room-level pointer drives every bubble.
- **Presence is never inferred.** A message arriving does not mark its sender online, and nothing
  polls.
- **`presenceEnabled: false` means "we cannot know", not "nobody is here."** Indicators are hidden
  entirely rather than showing everyone as offline.

Fixing the presence foundation also fixed a live defect: the connection registry had no reverse index
and relied on a 12-hour TTL, so a closed app looked online for half a day — and because the push
fan-out reads the same registry to decide who is offline, **those users' push notifications were
suppressed for up to 12 hours** (D-114).

---

## 8. Security model

- **Membership is the gate.** Every REST call and every `JoinRoom` re-checks it live; a hidden or
  non-member room answers **404, not 403** (D-018), so room existence does not leak.
- **Capabilities are server-computed** and never re-derived client-side.
- **Resource roles are queried live per request**, never trusted from a token claim (D-015).
- **The realtime token never reaches the browser as a durable credential**: web fetches it from a
  same-origin route handler, and `accessTokenFactory` re-invokes on reconnect so a refreshed token is
  picked up without tearing the socket down (D-109).
- **Uploads are validated server-side only**, and served `application/octet-stream`.
- **Errors are RFC7807** with `error` + `correlationId`, leaking no internals.
- Rate limiting: a global partitioned limiter plus a per-room sliding window in `ChatHub`.

Full detail: the **Security model — detail** section below.

---

## 9. Moderation model

Capability-driven: a moderator sees moderation actions because the **server** said so.

**The ladder (D-301).** `ChatMemberRole` is `{ Member, Moderator, Host }` and ordinal order **is** rank
order. Two predicates carry the entire model:

```
CanModerate(role)       => role >= Moderator
OutRanks(actor, target) => actor >  target      // strictly greater
```

The strict `>` is the whole protection model: a Moderator cannot mute, ban, or delete the messages of
another Moderator, and neither can touch a Host. A Host outranks everyone, so nothing special-cases them.

**Automatic Host comes from exactly four sources** — the event's creator, and an organization Owner,
Manager or Representative. Staff, Speaker, Judge, Mentor, Volunteer, Participant and ticket holders are
all Members (D-300). **Moderator is only ever an explicit promotion by a Host**, and promotion itself is
Host-only (`canManageModerators`): a Moderator who could promote could mint a peer, and the "cannot act on
an equal" rule would be bypassable in two steps.

`canManageRoom` (lock, post policy) is deliberately **not** implied by `canModerate` — a Moderator
moderates people, never the room itself.

- Pin / unpin, soft delete, mute (with expiry), ban — all over REST, all audited.
- Message deletion resolves the **sender's current** role, so someone promoted after posting is protected
  from that moment.
- Reports go through the shared `IReportService` into the existing moderation queue — chat did not
  get a second reporting path.
- Soft-deleted messages remain rows; clients render "Message deleted" rather than removing history.
- Locking a room (cancel, archive, event ended) makes it read-only for everyone, hosts included.

---

## 10. Notification model

Chat push reuses the existing FCM pipeline through `INotificationService` (D-107). **There is no
second notification service and no second provider.**

- Eligibility is server-side: the sender is never notified, muted and banned members are skipped, and
  members with a live connection are skipped via the presence registry.
- Payload carries `notificationType = "chat"` and the ids needed to deep-link to
  `/chats/{eventId}`; on Flutter `type` wins when a payload carries both keys.
- **Push failure never fails message delivery** — the message is already committed.

---

## 11. Reconnect behaviour

Identical on both clients:

1. `withAutomaticReconnect` re-establishes the socket (backoff 0 / 2 / 5 / 10 / 30s).
2. `JoinRoom` is re-invoked — membership is re-checked, and may now be denied (banned, refunded).
3. `?after=` catch-up recovers everything missed while disconnected.
4. The **roster is re-read** from the room endpoint rather than restoring what was held during the
   gap: presence during a gap is unknowable.

---

## 12. Offline behaviour

| | Flutter | Web |
|---|---|---|
| History cache | Hive (`kurx_cache`) | none |
| Outbox | persisted, flushed on room open and send | in-memory only |
| Online / typing | cleared on disconnect | cleared on disconnect |
| Read receipts | **preserved** | **preserved** |

Clearing presence but keeping receipts is the rule on both: a stale dot is presence we invented,
whereas a message that was read stays read. Neither client ever fabricates presence while offline.

---

## 13. Known production prerequisites

Design-complete, deliberately **not implemented**. The clients need no change when they arrive.

| Prerequisite | Status | Seam |
|---|---|---|
| Real object storage (S3 / R2 / Azure Blob) | **Still not implemented** — dev uses `LocalDiskStorage` with HMAC-signed presigns | `IStorage` |
| Real malware scanner | **SHIPPED (D-298)** — `FILE_SCANNER=clamav` speaks clamd's INSTREAM protocol over TCP and **fails closed**. `none` remains the default and provides no protection, named at every boot | `IFileScanner`, always invoked |
| Redis backplane | **SHIPPED** — configured via `REDIS_CONNECTION`; Production *refuses to boot* without it, because presence, the backplane and the per-instance limiters are all silently wrong under more than one instance | SignalR backplane |
| Sticky sessions | Not required — the Redis backplane removes the need | load balancer |
| Multi-instance deployment | Unblocked by the two above | no process-local state exists to block it |

> ⚠️ **Corrected 2026-08-08.** This table said the scanner and the backplane were "not implemented"/"not
> configured" long after both shipped — the scanner was verified against a live daemon with the EICAR
> signature, including the fail-closed path, on the same day this note was written. Only real object
> storage is genuinely still outstanding.

**Presence requires Redis. That is an operational dependency, not an application error** — without
it, messaging, attachments and persisted read pointers all work, and presence indicators are absent.

---

## 14. Future extension points

- **New event type** — add to the envelope; old clients ignore it by contract.
- **New chat mode** (Q&A, slow mode, scheduled open) — change `capabilities` server-side; no app
  release.
- **Reactions / search / threads** — additive, no contract change; `attachments: []` showed the
  pattern (reserved by D-104, filled by D-110 without a breaking change).
- ~~**Direct messages**~~ — shipped as D-264 (§6b), by making `EventId` nullable rather than by adding a second messaging system.
- **E2EE** — the substrate already exists (hardware-backed ES256 device keys, `DeviceCredential`,
  `TrustedDevice`, `RecoveryCode`); it is a product decision, not an architectural blocker.

---

## 15. Key architectural decisions

| D | Decision |
|---|---|
| D-104 | Contract frozen before clients: UUIDv7, `ClientMessageId`, keyset cursors, `LastReadMessageId`, capabilities, versioned envelope, reserved `attachments: []` |
| D-105 | Room lifecycle wired to event lifecycle |
| D-106 | Connection registry (reverse index deferred — later corrected by D-114) |
| D-107 | Chat push reuses the existing FCM pipeline |
| D-263 | `security` notifications are never suppressible; blocks are enforced symmetrically |
| D-264 | A DM is a `ChatRoom` with a null `EventId`; one room per pair by unique index; requests gate non-ally DMs |
| D-108 | Flutter chat client |
| D-109 | Web chat client; realtime token via same-origin route handler |
| D-110 | Attachments: presign → PUT → confirm, server-authoritative validation, `IFileScanner` |
| D-111 / D-112 | Flutter and web attachments |
| D-113 | Production-readiness audit |
| D-114 | Presence backend; `IPresenceService`; fixed the 12h push-suppression defect |
| D-118 | Flutter presence; `mounted` re-checks after every await |
| D-119 | Web presence; no chat Zustand store; memoised bubbles |
| D-120 | Milestone closure, parity fixes, decision-log governance |
| D-122 | Chat lifecycle derived from event lifecycle; `Archived` state; 7-day read-only window |
| D-123 | `EffectiveStatus` — lock enforced immediately, sweep persists |
| D-124 | Transitions idempotent (compare-and-set), persisted at first access, rule shared in `ChatLifecycle` |

---

## 16. Remaining technical debt

- **No message-list virtualization** on either client. Memoisation bounds the presence cost, not the
  initial mount of a very long room.
- **`MyChatView.LastActivity` is the event's `UpdatedAt`**, not real chat activity; clients sort on
  locally-observed activity where available.
- **The chat-specific 10/60s rate limit is WebSocket-only** — the REST send path runs at the generic
  300/min tier, and the `rate_limited` branch in `ChatEndpoints.Fail` is unreachable.
- **`NoOpFileScanner` provides no malware protection.**
- **Typing is not shown in the room list**, only inside an open room.
- **Web has no offline cache** (deliberate).

---

## Database schema


**Canonical source for chat tables.** `docs/DATABASE_TABLES.md` points here; do not restate this schema there.

> Phase 1 of D-104 has landed: migration `20260718120000_ChatContractFreeze`. Sections marked *FROZEN change* below are now live in the schema.

Architecture context: the **Backend architecture — detail** section below. Status markers (SHIPPED / FROZEN / DEFERRED) are defined there.

Tables were created in `20260708123509_AddInvitationsAndAnnouncements.cs:14-182`. The migration named `20260708123540_AddEventChat.cs` is **empty** — both `Up` and `Down` are no-ops (lines 11-20). The name is misleading; there is no functional impact and it is left untouched (append-only migration history).

EF configuration: `KurxDbContext.cs:981-1009`.

> ⚠️ **Rewritten 2026-08-08 from the EF model snapshot.** This page had drifted badly: it was missing every
> column added by D-264, D-293, D-295, D-296 and D-306, still called `chat_attachments` "DEFERRED" after it
> shipped, omitted two tables entirely, and one of its own correction notes had become **inverted** (see
> `chat_members.Role`). Verify against `KurxDbContextModelSnapshot.cs`, never against memory.

### `chat_rooms` — SHIPPED

One General room per event — **and** one Direct room per pair of users (D-264), which is why `EventId` is
nullable.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid PK | |
| `EventId` | uuid **nullable** FK → `events` (cascade) | unique **with `Kind`**, filtered to non-null — a Direct room has no event |
| `Kind` | text | `ChatRoomKind` = `General` \| `Announcements` \| `QA` \| `Staff` \| `Organizers` \| `Direct`. Only `General` and `Direct` are produced today |
| `PostPolicy` | text | `ChatPostPolicy` = `Everyone` \| `HostsOnly` |
| `Status` | text | `ChatRoomStatus` = `Active` \| `Locked` \| `Archived`. The room's **own lifecycle** (D-122) — not a member's filing, which is `chat_members.ArchivedAt` |
| `LockedAt` | timestamptz nullable | set on transition to `Locked` |
| `DirectLowUserId` | uuid nullable | D-264 — the pair, stored **ordered** so (A,B) and (B,A) are one row |
| `DirectHighUserId` | uuid nullable | unique `(DirectLowUserId, DirectHighUserId)` is what enforces one room per pair |
| `DmRequestState` | int nullable | `DmRequestState` — null for event rooms, so the field is meaningful only where a request gate exists |
| `DmInitiatedBy` | uuid nullable | who opened it; decides whose reply *accepts* the request (D-292) |
| `CreatedAt` | timestamptz | |

> Earlier documentation listed `ModeratorsOnly`. That has never existed. `Archived` **does** exist as a room
> status (D-122); an older note here denied it — corrected 2026-08-08.

#### FROZEN change — shipped (Phase 1)

| Column | Type | Notes |
|---|---|---|
| `Kind` | text, default `General` | `ChatRoomKind` = `General` (only value used today) |

Unique index moves `(EventId)` → `(EventId, Kind)`. Existing rows backfill to `General` by column default; **no data migration, no row movement**. Product behaviour is unchanged — one `General` room per event.

### `chat_members` — SHIPPED

| Column | Type | Notes |
|---|---|---|
| Column | Type | Notes |
|---|---|---|
| `Id` | uuid PK | |
| `RoomId` | uuid FK → `chat_rooms` (cascade) | |
| `UserId` | uuid FK → `users` | non-nullable — guests cannot join |
| `Role` | text | `ChatMemberRole` = `Member` \| `Moderator` \| `Host` (D-301). **Ordinal order is rank order**; stored as the member *name*, so inserting `Moderator` in the middle needed no migration and no backfill |
| `MutedUntil` | timestamptz nullable | **host mute** — stops this member POSTING. Not the reader's own notification mute below |
| `IsBanned` | bool | removed from the room; also evicts live sockets |
| `LastReadMessageId` | uuid nullable FK → `chat_messages` | **authoritative** for unread |
| `LastReadAt` | timestamptz | analytics only — see below |
| `LastDeliveredMessageId` | uuid nullable | D-295 — the middle receipt state, distinct from read |
| `PinnedAt` | timestamptz nullable | D-295 — **conversation** pinned in this reader's own list. Personal; says nothing to the room |
| `NotificationsMutedUntil` | timestamptz nullable | D-295 — this reader silenced their own notifications. An absolute instant, not a duration, so a sleeping device wakes correctly un-muted |
| `ArchivedAt` | timestamptz nullable | D-295/D-306 — filed out of this reader's active list. Personal and reversible; **not** `chat_rooms.Status == Archived` |
| `JoinedAt` | timestamptz | |

Indexes: unique `(RoomId, UserId)`; `(UserId)`; `(RoomId, Role)`.

> An older note here read *"Earlier documentation listed role `Moderator`. The enum value is `Host`."* That
> note is now **inverted**: D-301 added `Moderator` as the middle rung. It is corrected rather than deleted
> because the reasoning it preserved — that the roster once claimed a role the enum did not have — is
> exactly the failure this page keeps repeating.

**The three "mutes" are different things** and share only a word: `chat_members.MutedUntil` (a host
silences a member), `chat_members.NotificationsMutedUntil` (a reader silences their own notifications), and
`chat_rooms.PostPolicy = HostsOnly` (nobody but hosts may post).

#### FROZEN change — shipped (Phase 1)

| Column | Type | Notes |
|---|---|---|
| `LastReadMessageId` | uuid nullable FK → `chat_messages` | **authoritative** for unread |

`LastReadAt` is **retained, for analytics only** — it must never again be used to compute unread counts. Rationale: it is currently written from a client-supplied timestamp with no clamping (`ChatEndpoints.cs:10` → `ChatService.cs:155`), and two devices with skewed clocks have no correct merge rule. Message ids are server-assigned, monotonic under UUIDv7, and merge across devices by `max`.

### `chat_messages` — SHIPPED

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid PK | UUIDv7 since Phase 1; pre-existing rows hold v4 |
| `RoomId` | uuid FK → `chat_rooms` (cascade) | |
| `SenderId` | uuid nullable FK → `users` | null = system message |
| `Kind` | text | `ChatMessageKind` = `Text` \| `System` (`Enums.cs:48`) |
| `Body` | text | 1000-char limit enforced in app only (`ChatService.cs:132`) |
| `ReplyToMessageId` | uuid nullable FK → `chat_messages` | Accepted since chat shipped; the **UI** for it only landed 2026-08-08 |
| `EditedAt` | timestamptz nullable | D-293 — null = never edited. A no-op edit stamps nothing, so "edited" always describes a real change |
| `IsPinned` | bool | pinned **for the whole room** — a moderation act, unlike `chat_members.PinnedAt` |
| `PinnedUntil` | timestamptz nullable | D-296 — 1 h…30 d, default 7 d. **Expiry is evaluated at read time**, so there is no sweeper and every surface agrees on the moment a pin lapses |
| `ForwardedFromMessageId` | uuid nullable | D-295 — provenance. A null source name is honest: the original may be deleted or in a room this reader cannot see |
| `LinkUrl` · `LinkTitle` · `LinkDescription` · `LinkImageUrl` | text nullable | D-295 link card. The **host** is derived from `LinkUrl` at read time and is the one field a sender cannot fake — which is why clients render it |
| `IsDeleted` | bool | soft delete — `Body` cleared to `""` |
| `DeletedBy` | uuid nullable | **no FK** to `users` |
| `CreatedAt` | timestamptz | |

A deleted message reports no attachments, no reactions, no edit stamp, no forward provenance and no link
card: there is nothing left for any of them to describe.

Indexes: `(RoomId, CreatedAt, Id)` (keyset pagination); unique `(RoomId, ClientMessageId)`; partial
`ix_chat_messages_pinned` on `(RoomId)` filtered `WHERE "IsPinned" = true`; **GIN
`ix_chat_messages_body_fts` on `to_tsvector('english', "Body")`** (D-295) backing `websearch_to_tsquery`
search — the same idiom posts use (D-297), deliberately one idiom and not two.

### `chat_message_reactions` — SHIPPED (D-295)

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid PK | |
| `MessageId` | uuid FK → `chat_messages` (cascade) | |
| `UserId` | uuid FK → `users` | |
| `Emoji` | text | capped at 16 chars **and** validated as emoji-shaped — at least one non-ASCII rune, no ASCII letters. The length cap alone let a member put arbitrary text under every message, echoed to the whole room, outside the message pipeline and therefore unreportable |
| `CreatedAt` | timestamptz | |

Unique `(MessageId, UserId, Emoji)` — one of each emoji per person per message, so the toggle is idempotent.

### `chat_message_hides` — SHIPPED (D-293)

"Delete for me". The message itself is untouched — every other member still sees it, moderation still reads
it, the audit trail is unaffected; only this reader's queries filter it out.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid PK | |
| `MessageId` | uuid FK → `chat_messages` (cascade) | |
| `UserId` | uuid FK → `users` | |
| `CreatedAt` | timestamptz | |

Unique `(MessageId, UserId)` — hiding twice is a no-op. **Never broadcast**: nobody else's view changed,
and telling the room would leak a private filing decision. Honoured by the message list, search and the
shared-media grid alike.

> Earlier documentation listed an `Image` kind. The enum has only `Text` and `System` — chat is text-only. Corrected 2026-07-18 (D-104).

#### FROZEN changes — shipped (Phase 1)

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | generated by `Guid.CreateVersion7()` — k-sortable, serves as the cursor |
| `ClientMessageId` | uuid | client-supplied; **unique `(RoomId, ClientMessageId)`** |

**Index shipped as `(RoomId, CreatedAt, Id)`, not the `(RoomId, Id)` named in D-104.** Reason found during implementation: rows created before Phase 1 hold Guid **v4** ids, so ordering by `Id` alone would scramble the historical tail of every room. The sort key is therefore the `(CreatedAt, Id)` pair, which is a stable total order across the mixed table. The new index is a prefix-superset of the `(RoomId, CreatedAt)` index it replaces, so nothing that used the old one regresses. **The client contract is unaffected** — the cursor is still an opaque message id.

Ordering rationale: `CreatedAt` is not unique (`DateTime.UtcNow` has ~15ms resolution) and pagination previously pivoted on a strict `<` comparison, so same-timestamp messages were skipped or duplicated across pages. Covered by `ChatContractTests.Keyset_pagination_does_not_skip_or_duplicate_messages_with_identical_timestamps`.

### `chat_attachments` — SHIPPED (D-110/D-295/D-298)

> This section said **"DEFERRED (shape reserved)"** until 2026-08-08. It shipped, and the real shape
> differs from what was reserved: there is no `Kind` column (the content type carries that), and the row
> gained `RoomId`, `FileName`, `Width`/`Height`, `UploadedBy` and `DeletedAt`.

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid PK | |
| `RoomId` | uuid FK → `chat_rooms` | the room the key was issued for — checked on confirm, so an object cannot be attached across rooms |
| `MessageId` | uuid **nullable** FK → `chat_messages` | **nullable on purpose**: confirm happens *before* the message exists. Anything never claimed is swept by `ChatAttachmentCleanupJob`, or it would sit in storage forever |
| `StorageKey` | text | **unique** — which is what makes a concurrent confirm converge on one row instead of throwing. Scoped `chat/{roomId}/…`, and a key outside that prefix is refused |
| `FileName` | text | sanitised; path separators cannot escape the room prefix |
| `ContentType` | text | **re-derived from the bytes on confirm**, never trusted from the client. A declared type that contradicts the magic bytes is refused |
| `SizeBytes` | bigint | |
| `Width` · `Height` | int nullable | decoded server-side for images/video; null for documents and audio |
| `UploadedBy` | uuid FK → `users` | only the uploader may claim their own upload onto a message |
| `DeletedAt` | timestamptz nullable | soft delete; the sweep removes the object afterwards |
| `CreatedAt` | timestamptz | |

Indexes: unique `(StorageKey)`; `(MessageId)`; `(RoomId, CreatedAt)`; `(UploadedBy)`.

**The malware scan sits on confirm** (D-298): bytes → allow-list/magic-byte inspection → `IFileScanner` →
row. Any non-`Clean` verdict audits the rejection, deletes the object and refuses — so **no row is ever
written for an unscanned file**, and a scanner that is down blocks uploads rather than passing them.

### Known gaps — documented, not scheduled

These are recorded for completeness and are **not** part of the Phase 1 freeze:

- No `CHECK` constraint on `Body` length; the 1000-char limit is application-only.
- ~~No non-empty guard~~ — fixed in Phase 1: `body_required` rejects empty/whitespace bodies. The `CHECK` constraint is still absent; the limit remains application-only.
- `DeletedBy` has no foreign key to `users`.
- No table records moderation history; `MutedUntil` and `IsBanned` are mutable columns with no audit of prior state.

### Related tables owned elsewhere

- `reports` — abuse reports. `EntityType` already whitelists `chat_message` (`ReportService.cs:14`) with duplicate suppression (`:24`). Documented in `docs/DATABASE_TABLES.md`.
- `audit_log` — chat currently writes here directly via `db.AuditLogs.Add` (`ChatService.cs:183,245,257,303`) rather than the typed `IAuditWriter` spine (D-102). Noted in the **Security model — detail** section below.

---

## REST contract


**Canonical source for the chat REST contract.** `docs/api/README.md` points here.

> Phase 1 of D-104 has landed — the four contract changes below are implemented and covered by `Kurx.Tests/ChatContractTests.cs`.

Architecture: the **Backend architecture — detail** section below. Realtime: the **Realtime — SignalR** section below. Status markers are defined in the architecture doc.

All routes require authentication. Errors are RFC7807 `ProblemDetails` with `error` + `correlationId` extensions, mapped at `ChatEndpoints.cs:125-133`.

> **Direct messages (D-264) use every endpoint below.** A DM is a `ChatRoom` with a null `EventId`, so
> `/v1/chat/rooms/{roomId}/*` — send, history, attachments, read marking, delete — works on it
> unchanged. What is *new* is only room creation, the request gate and the per-user archive, which
> live under `/v1/dm/*` and are documented in
> [`docs/api/README.md` § Direct messages](README.md#direct-messages-d-264). Two additional send-path
> errors apply to direct rooms only: `blocked` and `dm_declined`.
>
> The one route that is event-shaped is `GET /v1/events/{eventId}/chat`; a direct room is addressed by
> room id instead (`IChatService.GetRoomByIdAsync`, which event chat now also routes through so there
> is one view builder rather than two).

### Shipped endpoints

Source: `ChatEndpoints.cs`.

**Rooms and membership**

| Method | Route | Notes |
|---|---|---|
| GET | `/v1/events/{eventId}/chat` | Room view for the caller, addressed by event |
| GET | `/v1/chat/rooms/{roomId}` | The same view addressed by **room** — the navigation primitive since D-292, and the only door a Direct room has |
| PATCH | `/v1/chat/rooms/{roomId}` | **Host only** (`canManageRoom`) — `postPolicy`, `status` |
| GET | `/v1/me/chats` | Caller's event rooms. **`?archived=`** selects which side of their own filing (D-306) |
| POST / DELETE | `/v1/chat/rooms/{roomId}/pin` | Pin the **conversation** in the caller's own list. Personal; not the message pin below |
| POST / DELETE | `/v1/chat/rooms/{roomId}/mute` | Silence the caller's **own** notifications. Not the host mute below |
| POST / DELETE | `/v1/chat/rooms/{roomId}/archive` | File the room out of the caller's active list, and back. Works for an event room; DMs use the `/v1/dm` route |

**Messages**

| Method | Route | Notes |
|---|---|---|
| GET | `/v1/chat/rooms/{roomId}/messages` | `?before=<cursor>&after=<cursor>&limit=` — opaque cursors, one direction at a time |
| POST | `/v1/chat/rooms/{roomId}/messages` | `{ body, replyToMessageId?, clientMessageId?, attachmentIds?, linkPreview? }` — idempotent per room |
| PATCH | `/v1/chat/messages/{messageId}` | Edit own message (D-293). Stamps `edited_at`; a no-op edit stamps nothing |
| DELETE | `/v1/chat/messages/{messageId}` | Unsend — sender within 15 min, or a moderator who **outranks** the sender |
| DELETE | `/v1/chat/messages/{messageId}/for-me` | Hide for the caller only (D-293). Never broadcast — nobody else's view changed |
| POST | `/v1/chat/messages/{messageId}/reactions` | `{ emoji }` — toggles. Emoji-shaped values only, so the field cannot carry unmoderatable text |
| POST | `/v1/chat/messages/{messageId}/forward` | `{ targetRoomId, clientMessageId? }` — membership of the target is re-checked |
| POST / DELETE | `/v1/chat/messages/{messageId}/pin` | Pin a **message** for the whole room. `{ durationHours? }` — 1 h…30 d, default 7 d (D-296) |
| POST | `/v1/chat/messages/{messageId}/report` | `{ reason }` — files into the platform moderation queue |
| POST | `/v1/chat/rooms/{roomId}/read` | `{ lastReadMessageId }` — monotonic |
| POST | `/v1/chat/rooms/{roomId}/delivered` | `{ messageId }` — the middle receipt state (D-295) |
| GET | `/v1/chat/search` | `?q=&roomId=&limit=` — Postgres FTS. Membership is **inside** the query, so the result count cannot leak a room the caller is not in |

**Moderation** — the D-301 ladder, `Member < Moderator < Host`

| Method | Route | Who |
|---|---|---|
| POST | `/v1/chat/rooms/{roomId}/members/{userId}/mute` | Moderator **or** Host, and only over someone they outrank |
| POST / DELETE | `/v1/chat/rooms/{roomId}/members/{userId}/ban` | Same ladder. Removal also evicts live sockets |
| POST / DELETE | `/v1/chat/rooms/{roomId}/members/{userId}/moderator` | **Host only** (`canManageModerators`) — a Moderator who could promote could mint a peer |

**Attachments** (D-110/D-298) — `presign` → PUT → `confirm`, with the malware scan on confirm

| Method | Route | Notes |
|---|---|---|
| POST | `/v1/chat/rooms/{roomId}/attachments/presign` | Returns the upload ticket |
| POST | `/v1/chat/rooms/{roomId}/attachments/confirm` | Type re-derived from the bytes, then scanned. **Fails closed**: `file_infected` / `scan_unavailable`, and no row is written |
| GET | `/v1/chat/attachments/{attachmentId}/url` | Short-lived signed URL, minted per read; membership re-checked |
| GET | `/v1/chat/rooms/{roomId}/media` | Shared-media grid — live attachments, newest first |

> **Two different pins.** `/v1/chat/rooms/{id}/pin` files a *conversation* in the caller's own list and is
> visible to nobody else; `/v1/chat/messages/{id}/pin` pins a *message* for the whole room and is a
> moderation act. They share a word and nothing else.

> Earlier documentation listed `/v1/events/{id}/chat/room` and `/v1/chat/rooms/{id}/members`. Neither exists — the room route has no `/room` suffix and there is **no members-listing endpoint**. Corrected 2026-07-18 (D-104).

> Source line references were removed from this table on 2026-08-08: they had drifted with every edit to
> `ChatEndpoints.cs`, and a wrong line number costs more than no line number.

#### Error mapping

| `error` | Status |
|---|---|
| `forbidden`, `banned`, `muted`, `hosts_only`, `delete_window_expired` | 403 |
| `cannot_moderate_peer`, `cannot_change_host` | 403 |
| `file_infected`, `scan_unavailable` | 403 |
| `not_found` | 404 |
| `conflict` (concurrent promote/demote lost the race) | 409 |
| `invalid_pin_duration`, `query_too_short` | 400 |
| `room_locked` | 423 |
| `room_archived` | 423 |
| `rate_limited` | 429 |
| anything else | 400 |

`rate_limited` is currently unreachable from the service — 429s originate from the global limiter instead (see the **Security model — detail** section below).

### Contract changes — shipped (Phase 1)

These four exist because they are effectively impossible to change once Web and Flutter cache data against them. Nothing else in the contract changes.

#### 1. Cursor pagination — opaque cursor, both directions

```
GET /v1/chat/rooms/{roomId}/messages?before=<cursor>&limit=50
GET /v1/chat/rooms/{roomId}/messages?after=<cursor>&limit=50
```

- The cursor is an **opaque string**. Clients must not parse, order, or construct it. It happens to be the UUIDv7 message id; that is an implementation detail deliberately hidden so the underlying cursor can change without a client release.
- `after` is the delta-synchronisation primitive: a reconnecting client asks for everything since its last known message. **Without it, offline sync is not expressible** — today's contract is `before`-only (`ChatEndpoints.cs:39`).
- `limit` stays clamped 1..50 (`ChatService.cs:105`).

#### 2. Idempotent send via `ClientMessageId`

```
POST /v1/chat/rooms/{roomId}/messages
{ "clientMessageId": "<uuid>", "body": "…", "replyToMessageId": null }
```

The server treats `(roomId, clientMessageId)` as an idempotency key: a repeat returns the original message with 200 rather than creating a duplicate. This is what makes an offline retry queue safe and lets optimistic UI reconcile its placeholder against the server row. Same pattern as `OutboxMessage.IdempotencyKey` (`AuthIdentity.cs:135`).

#### 3. Capabilities object on the room view

`ChatRoomView` gains a server-computed capabilities block:

```json
{
  "roomId": "…", "status": "Active", "kind": "General",
  "capabilities": {
    "canPost": true, "canReply": true, "canUpload": false,
    "canPin": false, "canDelete": false, "canModerate": false,
    "canMentionAll": false
  }
}
```

Clients render from `capabilities` and **never** re-derive permissions from role + policy. This is the single decision that insulates both clients from every future chat mode — Q&A, slow mode, scheduled open, announcements-only all become server-side changes with no client release.

`postPolicy` and `myRole` remain in the payload for display purposes only.

> This is a DTO shape decision, not a permission engine. `ChatPostPolicy` stays the two-value enum it is (`Enums.cs:45`). A configurable policy model is **DEFERRED** — it can be added later without touching the wire contract, which is precisely why it is not being built now.

#### 4. Reserved `attachments` array

Every message view carries an `attachments` array. It was reserved as always-empty by D-104 and **populated for real since D-110** — which is exactly why reserving the cardinality mattered: a scalar field would have broken both clients' renderers.

Each entry is `{ id, url, fileName, contentType, sizeBytes, width, height }`. `url` is a short-lived signed download URL minted per read; `fileName` is server-sanitized and is the only trustworthy label (the storage receiver serves `application/octet-stream` with no `Content-Disposition` by design).

#### Message view (frozen shape)

```json
{
  "id": "<uuidv7>",
  "clientMessageId": "<uuid|null>",
  "roomId": "…", "senderId": "…", "senderName": "…", "senderRole": "Member",
  "kind": "Text", "body": "…", "replyToMessageId": null,
  "isPinned": false, "isDeleted": false,
  "attachments": [],
  "createdAt": "…"
}
```

#### Read marking

```
POST /v1/chat/rooms/{roomId}/read
{ "lastReadMessageId": "<uuidv7>" }
```

Replaces `{ at: <timestamp> }`. The current endpoint accepts a client-supplied clock value and writes it through unclamped (`ChatService.cs:155`); message ids are server-assigned and merge across devices by `max`. `LastReadAt` is retained for analytics only and no longer computes unread.

### Deliberately NOT in the v1 contract

This section records what the **Phase-1 frozen contract** deliberately excluded. Almost all of it has since
shipped — additively, exactly as the freeze intended — and is struck through here rather than deleted,
because the prediction holding is the point:

- ~~**Server-side full-text message search**~~ — **shipped as D-295**. Excluded originally because it would
  make E2EE a breaking change later; that trade was taken knowingly, and E2EE is not on the roadmap.
  `websearch_to_tsquery` + a GIN index, with **membership inside the query** so the result *count* cannot
  leak a room the caller is not in.
- ~~Typing, presence~~ — **shipped as D-114/D-119**. ~~Reactions, edit, forward~~ — **shipped as
  D-293/D-295**, alongside delete-for-me, link previews, shared media, delivery receipts and expiring pins
  (D-296). Every one landed additively; no endpoint in this contract changed shape.
- **Threads remain out of scope, by design.** A reply quotes a message (`ReplyToMessageId`); it does not
  open a sub-conversation. `ReplyToMessageId` shipped with chat and reached a **UI** only on 2026-08-08.
- ~~Direct messages~~ — **shipped as D-264**, additively exactly as predicted: no endpoint changed shape,
  `EventId` simply became nullable. Group DMs remain out of scope (the pair is structural — two id
  columns, not a membership set).

### Breaking-change summary

| Change | Breaking? |
|---|---|
| `Id` becomes UUIDv7 | No — still a `uuid`; ordering only becomes correct |
| `clientMessageId` on send | No — new optional field, additive |
| `after` cursor | No — additive parameter |
| `attachments: []` | No — additive field |
| `capabilities` | No — additive field |
| **`/read` body: `at` → `lastReadMessageId`** | **Yes** — see the migration plan in the Phase 1 report |

Only the last one breaks, and it breaks nothing in practice: no client calls it today.

---

## Realtime — SignalR


**Canonical source for the chat realtime transport.** Architecture context: the **Backend architecture — detail** section below.

### Shared platform, not chat-specific

`IRealtimeBroadcaster` (`Kurx.Application/Abstractions/IRealtimeBroadcaster.cs`) is already the platform-wide realtime seam — nine methods covering scan feeds, sales, chat, notifications, unread counts, badge unlocks, wallet, analytics, and login status. Hub implementations live in `Kurx.Api`; Infrastructure services depend only on the interface and stay framework-agnostic.

**This abstraction is approved as-is and must not be restructured.** Announcements, live dashboards, check-in counters, polls, leaderboards, and emergency alerts all extend it by adding a method — none require an architectural change. Do not create a second realtime abstraction for chat or for any of those features.

### Hubs

| Hub | Route | Registered |
|---|---|---|
| `ScanHub` | `/hubs/scan` | `Program.cs:344` |
| `SalesHub` | `/hubs/sales` | `:345` |
| `ChatHub` | `/hubs/chat` | `:346` |
| `NotificationHub` | `/hubs/notifications` | `:347` |
| `LoginHub` | `/hubs/login` | `:350` — anonymous by design (AM9) |

All except `LoginHub` require authentication. Browsers cannot set an `Authorization` header on a WebSocket, so the access token rides the query string: `wss://…/hubs/chat?access_token=<token>`.

### ChatHub — SHIPPED

Source: `ChatHub.cs`, `[Authorize]`.

| Method | Behaviour |
|---|---|
| `JoinRoom(roomId)` | Membership + ban re-checked against the database at connect (`:21-25`); throws `HubException("forbidden")` otherwise |
| `LeaveRoom(roomId)` | Removes the connection from the group (`:28`) |
| `SendMessage(roomId, body, replyToId?)` | Rate-limit check, then delegates to `ChatService` (`:31-40`) |

The membership re-check on join is the D-017 invariant: group membership is never trusted from a token claim.

> Earlier documentation credited `ChatHub` with delete, pin, react, typing, and read-receipt broadcast methods. **None of these exist** — the hub has exactly the three methods above. Corrected 2026-07-18 (D-104).

Group naming: `chat:{roomId}` (`ChatHub.cs:25`, `SignalRBroadcaster.cs:21`).

### Write path

`ChatService` persists first, then broadcasts (`ChatService.cs:147`). `ChatHub.SendMessage` deliberately does **not** broadcast — the comment at `ChatHub.cs:39` records this — so a message sent over the hub and one sent over REST fan out through exactly one path and cannot double-send.

Server → client events currently emitted: `MessageReceived`, `MessageDeleted`, `RoomUpdated`, `MessagePinned`, `MemberMuted`.

### Versioned envelope — shipped (Phase 1)

Broadcast payloads gain a stable envelope:

```json
{ "v": 1, "type": "MessageReceived", "roomId": "…", "id": "<uuidv7>", "payload": { … } }
```

Two properties are load-bearing:

- **`id`** — the UUIDv7 of the message the event concerns. A client that reconnects can compare it against its last cached id and detect a gap, then reconcile with `GET …/messages?after=<cursor>`. **Without an id in the envelope, realtime and offline sync cannot be made consistent**, because a client that missed an event has no way to know it did.
- **`v`** — protocol version, so the envelope can evolve without a coordinated client release.

Clients must ignore unknown `type` values and unknown payload fields — that forward-compatibility rule is what lets new event types ship server-first.

### Presence, typing and read receipts (Phase 5, D-114)

Three independent capabilities over the existing envelope. Old clients ignore unknown types, so these shipped server-first.

| Event | Payload | Notes |
|---|---|---|
| `PresenceChanged` | `{ userId, online }` | Emitted only on the transition — a second tab does not re-announce |
| `TypingChanged` | `{ userId, userName, isTyping, ttlSeconds }` | Never persisted, never pushed; receivers clear on `ttlSeconds` even if "stopped" is lost |
| `ReadReceiptChanged` | `{ userId, lastReadMessageId }` | Emitted only when the persisted pointer actually advances |

**Presence requires Redis.** `IPresenceService` decides availability in one place; without `REDIS_CONNECTION` it is a null object and presence is cleanly off, while messages, attachments and read receipts continue working. There is deliberately no in-memory fallback — process-local presence would be wrong the moment a second instance ran.

Three keys: `chat:conns:{room}:{user}` (connection ids, refcount), `chat:online:{room}` (roster in one round trip), `chat:conn-rooms:{connId}` (**the reverse index that makes disconnect cleanup possible**). TTL is 120s and is a crash backstop only — normal cleanup is `OnDisconnectedAsync`; a `Heartbeat()` hub method refreshes it.

### Connection lifecycle — SHIPPED (Phase 5, D-114)

`ChatHub` now overrides `OnDisconnectedAsync`, releasing every room the connection joined and announcing offline only when the user's last connection goes. The pattern mirrors `NotificationHub.cs:11-22`:

```csharp
OnConnectedAsync    → Groups.AddToGroupAsync(ConnectionId, $"user:{userId}")
OnDisconnectedAsync → Groups.RemoveFromGroupAsync(ConnectionId, $"user:{userId}")
```

Reuse that shape rather than writing new connection-management code.

**Ban eviction — SHIPPED (Phase 2B, D-106; moved onto `IPresenceService` in D-114).** `BanMemberAsync` calls `IRealtimeBroadcaster.EvictFromChatAsync`, which broadcasts `MemberBanned` and then removes the user's live connections from the `chat:{roomId}` group. Setting `IsBanned` alone only stopped the *next* request; an already-connected client kept receiving every message until it happened to reconnect.

Connection ids are recorded per `(room, user)` in a TTL-expiring Redis set on `JoinRoom` (`ChatHub.ConnectionSetKey`) and removed on `LeaveRoom`. There is deliberately no reverse index and no `OnDisconnected` bookkeeping — removing a connection that has since died is a harmless no-op, so the registry can rely on the TTL. **Ceiling:** without Redis there is no registry, so eviction degrades to the `MemberBanned` broadcast alone and a hostile client could keep listening until it reconnects. Server-side authorization still refuses every write.

### Scaling

**Redis backplane** is registered only when `REDIS_CONNECTION` is set (`Program.cs:83-88`); otherwise SignalR runs single-instance and the chat rate limiter degrades to always-allow (`ChatHub.cs:44-48`).

**Sticky sessions are required.** Per Microsoft's SignalR scale-out guidance (`aspnetcore/signalr/redis-backplane.md`, `aspnetcore/signalr/scale.md`), a Redis backplane in a server farm requires the load balancer to pin a client to one server. No ingress configuration in this repository does so today. This blocks multi-instance deployment regardless of user count and is an operations task, not an architecture change.

#### Known bottlenecks, in order of onset

1. `GetMyChatsAsync` (`ChatService.cs:194-217`) issues two queries **per room inside a loop** (`:212`, `:213`).
2. Unread is a live `COUNT(*)` (`:81`, `:212`) rather than a stored counter — the `LastReadMessageId` change makes a counter feasible later.
3. No connection accounting at all, pending the lifecycle overrides above.
4. The rate limiter issues a `SortedSetLengthAsync` after its transaction (`ChatHub.cs:59-60`) — an extra Redis round-trip per message.

None of these require a contract or schema change to fix, which is why they are outside the Phase 1 freeze.

### Archived rooms (D-122)

An archived room still joins its SignalR group — history and moderation events must reach an open
client — but registers **no presence at all**, emits no typing, and sends no notifications. Reading an
archive is not attendance.

---

## Security model — detail


**Canonical source for chat security.** `docs/security/overview.md` points here. Architecture context: the **Backend architecture — detail** section below.

### Authorization

Every chat REST route calls `RequireAuthorization()` (`ChatEndpoints.cs`), and `ChatHub` carries `[Authorize]` (`ChatHub.cs:11`).

Chat permissions are **room-scoped**, derived from `ChatMember.Role`, and are deliberately independent of the org resource roles in D-015. Membership is re-read from the database on every operation — never trusted from a token claim:

| Operation | Check | Source |
|---|---|---|
| Join hub group | member exists and not banned | `ChatHub.cs:21-25` |
| Read history | member exists, not banned | `ChatService.cs:101-103` |
| Send | member, not banned, not muted, policy allows | `:127-131` |
| Delete | sender within 15 min, or host | `:168-175` |
| Pin / mute / ban / delete another's message | **Moderator or Host**, and only over someone they outrank | `CanModerate` + `OutRanks` (D-301) |
| Promote / demote a moderator · lock room · post policy | **Host only** | `IsHostAsync` — `canManageModerators` / `canManageRoom` |

The hub group-join re-check is the D-017 invariant.

**Who is seeded as `Host` is decided by `IEventAuthority`, not by the membership roster (D-272).**
`AddEventHostsAsync` seeds the event's **owner** — by owning the event, with no organization seat behind it
— plus every seat in `EventAuthority.ChatHostRoles` (Owner / Manager / Representative). A `Finance` seat is
not seeded: it holds money authority over the organization and nothing front-of-house. Previously both this
and `RemoveStaffMemberAsync` read `db.Memberships` directly, which granted `Finance` and gave a
personally-represented event's creator `Host` only through the synthetic self-representation row.

> ⚠️ **Corrected 2026-08-08.** This paragraph seeded "every seat holding `EventPermission.ModerateAudience`
> (`Staff` and above)" and the table above gated pin/mute/ban on `IsHostAsync`. Both describe the
> **pre-D-300/D-301** world. A `Staff` seat is now a **Member**, and `RemoveStaffMemberAsync` protects at
> `EventAuthorityLevel.Manager` — because a seat that no longer *confers* Host must no longer *protect* one
> either, or revoking an assignment would leave a Host row this model says cannot exist. `ChatHostRoles`
> (≥ Manager) and `ModeratorRoles` (≥ Staff) are separate arrays for exactly this reason; chat borrowed the
> latter before D-300, which is how an operational seat came to carry ban powers.

### Rate limiting

Two independent layers. Both are real; earlier internal review incorrectly reported that REST was unlimited.

**Global limiter** — `Program.cs:130-181`, applied via `app.UseRateLimiter()` (`:286`). Partitioned sliding window covering **every HTTP request**: 300/min per authenticated user (`:163-171`), 60/min per anonymous IP (`:174-180`), RFC7807 429 with `Retry-After` (`:132-145`).

**Chat-specific limiter** — `ChatHub.cs:42-62`. Redis sorted-set sliding window, 10 messages / 60s per user per room. Falls back to always-allow when Redis is unconfigured (`:44-48`).

**Architectural gap:** `UseRateLimiter` is HTTP middleware and does not intercept hub method invocations on an established WebSocket — which is why the hub carries its own limiter. The inverse is also true: `POST /v1/chat/rooms/{id}/messages` bypasses the *chat-specific* limit and runs at the generic 300/min tier, 30× the intended rate. The fix is to move the limiter into `ChatService` so both transports share one path. Not a Phase 1 contract item.

### Moderation and reporting

`ReportService` is the platform moderation system: a reviewable queue with `CreateAsync` / `ListAsync(status)` / `ResolveAsync(reviewerId, dismiss)` (`IReportService.cs:10-16`), duplicate suppression (`ReportService.cs:24`), and an entity whitelist that **already includes `chat_message`** (`:14`).

**SHIPPED (Phase 2B, D-106):** `ChatService.ReportMessageAsync` now calls `IReportService.CreateAsync(reporterId, "chat_message", messageId, reason, ...)`. Chat reports appear in the admin triage queue and can be resolved or dismissed like any other subject, and the duplicate guard (one open report per reporter+subject) applies — `POST /v1/chat/messages/{id}/report` returns **409 `already_reported`** on a repeat, matching `POST /v1/reports`.

Previously it wrote an `AuditLog` plus per-host `Notification` rows directly, so reports were invisible to moderation staff and the whitelisted `chat_message` entity type went unused.

### Audit logging

**SHIPPED (Phase 2B, D-106):** chat moderation writes through the D-102 typed spine — `audit.Write(new AuditEvent(...))` with structured `Before`/`After` — for `chat.message_delete`, `chat.mute`, `chat.ban` and `chat.unban` (`chat.unban` was previously not audited at all). No raw `db.AuditLogs.Add` remains in `ChatService`.

Because `IAuditWriter.Write` stages the row on the caller's unit of work, the audit entry now commits in the **same transaction** as the change it describes. `DeleteMessageAsync` previously issued two separate `SaveChanges` calls, so a failure between them left a deleted message with no audit trail.

**JSON injection closed.** Two `DetailsJson` values were built by interpolating user input into JSON string literals. The report reason is now stored as a column value by `IReportService`, and notification payloads are serialised by `NotifyAsync` via `JsonSerializer` — a quote in a report reason can no longer corrupt or forge a record. Regression-tested by `ChatModerationTests.A_reason_containing_quotes_cannot_corrupt_the_stored_record`.

### Attachment security — SHIPPED backend (Phase 4, D-110)

Every authoritative check runs on **confirm**, the first moment the server can see the object:

| Check | Rule |
|---|---|
| Content type | Allow-list only — images, PDF, OOXML, text/CSV, ZIP. Never a deny-list. |
| Extension | Must be one the content type may legitimately carry (blocks `invoice.pdf.exe`). |
| Magic bytes | Must agree with the declared type; a PE binary uploaded as `image/png` is refused. Text has no signature, so embedded NULs are the discriminator. |
| Size | 25 MB, enforced at presign, at the receiver while streaming, and again on inspection. |
| Ownership | Key must be prefixed `chat/{roomId}/`; only the uploader may claim it; only once. |
| Images | Decoded with ImageSharp — a file it cannot read is not an image, whatever the header says. |

Client-declared metadata is a hint throughout. If a declaration is wrong but the bytes match some other allowed type, the bytes win; matching nothing is refused.

**Downloads** are short-lived signed URLs minted per read, never stored, with room membership re-checked on every mint — so a ban revokes access to files the member could previously fetch. The storage receiver always serves `application/octet-stream`, because letting a stored object choose how a browser interprets it is stored XSS.

**Presigned URLs are genuinely signed.** `LocalDiskStorage` had been returning URLs to a route that did not exist, so no upload in the product had ever worked. The receiver added in D-110 verifies an HMAC over `key|exp|maxBytes|contentType` in fixed time; a GET signature cannot be replayed as a PUT.

#### Malware scanning

`IFileScanner` returns `{ Clean, Infected, ScanFailed, Unsupported }` and the pipeline always calls it. Any non-`Clean` verdict audits the rejection, deletes the object, and refuses the confirm.

**`FILE_SCANNER=clamav` is real scanning** (D-298) — clamd's INSTREAM protocol over TCP, verified against a live daemon with the EICAR test file. It **fails closed**: a refused connection, a timeout, a truncated reply or an unrecognised answer all map to `ScanFailed`, so a scanner that is down *blocks* uploads rather than passing them unscanned. Plan for clamd's availability; do not work around it.

**`FILE_SCANNER=none` remains the default and provides no protection**, always returning `Clean`. It is development-only, and the startup summary names it on every boot. Turning protection on is a config change and nothing else — that was the point of always having a call site.

The magic-byte checks in `AttachmentPolicy` are a **format** gate, not a malware gate: they prove a file is shaped like the type it claims. The two are independent and both are required.

### Not implemented

Spam and abuse detection, flood protection beyond the rate limits above, replay protection, shadow bans, and blocked users. Moderation-action *history* is now readable from `audit_log` (mute/ban/unban/delete carry structured before/after), though `ChatMember` still stores only current state. Message-level duplicate suppression arrived with `ClientMessageId` (Phase 1).

### Transport and storage encryption

TLS in transit at the ingress. `chat_messages.Body` is plain `text` — no application-level encryption at rest. The server reads plaintext for the length check (`ChatService.cs:132`), the last-message preview (`:213`), and delete blanking (`:178`).

### E2EE readiness — DEFERRED, foundation strong

The trusted-device authentication rail already supplies four MLS prerequisites, all production-grade:

| Prerequisite | Existing asset |
|---|---|
| Device identity keys | Hardware-backed ES256 — AndroidKeyStore + StrongBox, iOS Secure Enclave; private key provably never leaves the device (`mobile/lib/core/security/device_key_service.dart:14-17`) |
| Server key registry | `DeviceCredential.PublicKeySpki` (`AuthIdentity.cs:44`) |
| Verification / stable key ids | `DeviceSignatures.VerifyEs256`, `Thumbprint` |
| Device registry + revocation | `TrustedDevice` (`AuthIdentity.cs:22`) |
| Multi-device model | `AuthSession.TrustedDeviceId` (`:77`) |
| Recovery | `RecoveryCode` (`:98`) + `RecoveryCodeService` |

#### Smallest additions required before MLS

1. **A key-agreement keypair per device.** The existing key signs only — `device_key_service.dart:39` exposes `sign()` with no `deriveKey`. MLS/HPKE requires ECDH (X25519 or P-256 ECDH). The enrollment plumbing already exists; this adds a second key alongside the identity key.
2. **A device dimension on chat membership.** `ChatMember` is user-scoped; fan-out must become per-device. Additive table.
3. **Group state storage** for MLS ratchet trees. Rotation, forward secrecy, and backward secrecy come from MLS itself.
4. **Per-message attachment keys**, since `IStorage` is server-side.

`ChatMessage.Body` needs **no** structural change: MLS uses sender keys, so a message is one ciphertext blob, not per-recipient copies.

**Use MLS (RFC 9420).** Its tree-based group key agreement suits the high-churn membership of an event room. Do not design custom cryptography.

#### The one constraint binding Phase 1

**Server-side full-text message search must not enter the v1 API contract.** Promising it makes E2EE a breaking change later. Every other E2EE prerequisite is additive. Recorded in the **REST contract** section below.

### Authorization across lifecycle states (D-122 – D-124)

Access follows the event, and every gate resolves the state through the one shared rule
(`ChatLifecycle.EffectiveStatus`) rather than reading the stored status:

| Event state | Access |
|---|---|
| Draft, pending approval, rejected | **No room exists.** Nothing to authorize |
| Published / running | Eligible participants only — membership re-checked live on every request and every `JoinRoom` |
| Ended (`Locked`) | Read history, open attachments, report. No sending, no uploading |
| Ended + 7 days (`Archived`) | Read history. No sending, no uploading, no presence, no typing, no notifications. Participant state frozen — no mute, ban or membership change |

Moderation survives archiving: a host can still delete an offending message, because a report filed on
the last day of the read-only window has to remain actionable. Participant changes do not, because
muting somebody in a room nobody can post in achieves nothing.

**Enforcement never waits for the sweep.** The state is derived on every gate, so a room whose event
ended a minute ago already refuses messages, uploads, typing and presence — the hourly sweep only
makes that durable. A transition is a compare-and-set, so concurrent callers cannot double-apply it.

---

## Backend architecture — detail


**Status:** Shipped end to end (D-104 … D-120). Backend, Flutter and web all carry messaging, attachments and presence. The canonical cross-platform reference is [`docs/EVENT_CHAT_ARCHITECTURE.md`](../EVENT_CHAT_ARCHITECTURE.md); this document remains the backend architecture detail.

> The "no client is wired on either platform" line here was correct on 2026-07-18 and survived the phases that built both clients. Corrected 2026-07-19 (D-120).

This is the entry point for chat. Detail lives in one place each — do not duplicate it here:

| Topic | Owning document |
|---|---|
| Tables, columns, indexes | the **Database schema** section below |
| REST contract | the **REST contract** section below |
| Hub, groups, envelope, scaling | the **Realtime — SignalR** section below |
| Authorization, moderation, MLS readiness | the **Security model — detail** section below |
| Flutter offline contract | the **Flutter client** section below |
| Web client | the **Web client** section below |

Throughout these documents:

- **SHIPPED** — exists in code today, with a file:line reference.
- **FROZEN** — approved contract. Items marked *FROZEN → shipped* landed in Phase 1 and are verified by `Kurx.Tests/ChatContractTests.cs`.
- **DEFERRED** — deliberately not built; additive later, no migration required.

### Core model

**Event → ChatRoom.** One event has exactly one chat room. Users never create rooms; event membership determines chat membership.

**SHIPPED:** `ChatRoom` carries a unique index on `(EventId, Kind)`, and only `General` is ever created — so one room per event. `ChatMember` and `ChatMessage` both key on `RoomId` (`KurxDbContext.cs:994`, `:1006`) — never on `EventId`.

That indirection is the load-bearing property of this design: because no child table references `EventId`, adding further rooms per event later is an index change, not a data migration.

**FROZEN → shipped:** `ChatRoom.Kind` (default `General`), with the unique index moved from `(EventId)` to `(EventId, Kind)`. Today exactly one `General` room per event is created, so the product behaviour is unchanged. Additional kinds (Announcements, Q&A, Staff, Organizers) are **DEFERRED** — the column exists so that adding them never requires a breaking migration.

### Room lifecycle

**SHIPPED (Phase 2A):** the room is created at **publish**, from `EventService.TransitionAsync` — the single lifecycle gate (`EventStatusWorkflow.cs:11-25`). There is deliberately no second publish flow. The order paths still call `EnsureRoomExistsAsync`, which is now a no-op for a published event and remains the safety net for events published before this change.

| Event transition | Chat effect |
|---|---|
| -> `Published` | Room created; the event's **owner** and every `ChatHostRoles` seat (Owner/Manager/Representative) seeded as **Hosts**. A `Staff` seat and every accepted assignment join as **Members** (D-300) |
| -> `Cancelled` | Room locked immediately, system notice posted |
| -> `Archived` | Room locked immediately, system notice posted |
| -> `Closed` | No immediate effect — the sweep locks it 7 days after the event ends |

`ChatRoomStatus` is `{ Active, Locked, Archived }` — the third value was added by D-122 and this line still
denied its existence. Locked at the event's end, Archived 7 days later; an archived room keeps its history
readable and freezes everything else, including its participant list.

Distinct from a **member's** archive (D-295/D-306), which is `ChatMember.ArchivedAt` — personal filing,
visible to nobody else, and reversible. The room's status belongs to the event's clock; the member's
belongs to their inbox. They share a word and nothing else.

`LockExpiredRoomsAsync` is now scheduled hourly as `lock-expired-chat-rooms` (`LockExpiredChatRoomsJob`). It selects only `Active` rooms, so re-running locks nothing twice. Cancellation and archive lock immediately; the sweep is the catch-all for events that merely ended. Both paths share one private `LockRoomAsync` helper, so immediate and swept locking cannot drift.

**Retention and deletion are NOT implemented** — no retention policy exists anywhere in the repo, and inventing one is scope this phase does not own.

### Role lifecycle

> ⚠️ **Rewritten 2026-08-08.** This section described the Phase 2A model, in which a `Staff` seat and every
> accepted assignment became a **Host**, and named `AddAcceptedStaffAsHostsAsync` — a method that no longer
> exists. **D-300** removed automatic Hosts and **D-301** replaced them with explicit Moderators; neither
> updated this page, and the same drift left four tests asserting the old model. Do not restore the table
> below from git history.

`ChatMemberRole` is `{ Member, Moderator, Host }` — ordinal order **is** rank order (D-301).

There is still exactly one insertion path into `chat_members` (`ChatService.AddMemberAsync`), which is
**upgrade-only**: it never lowers an existing row.

| Caller | Role | When |
|---|---|---|
| `TransitionAsync` -> `AddEventHostsAsync` | `Host` | Publish — the event's creator + `ChatHostRoles` seats |
| `TransitionAsync` -> `AddAcceptedStaffAsMembersAsync` | `Member` | Publish — acceptances made while still a draft |
| `ParticipantService` -> outbox `event.chat_join` | `Member` | A programme participation is accepted |
| `OrderService` x3 | `Member` | Ticket issuance |
| `TicketTransferService.ClaimAsync` | `Member` | Transfer claimed |
| `ChatService.SetModeratorAsync` | `Moderator` | **Explicit promotion by a Host** (D-301) |
| `ChatService.SyncOrgAuthorityAsync` (outbox `org.authority_changed`) | `Host` / `Member` | Organization authority changes on an already-published event (D-304) |

**Automatic Host comes from exactly four sources** — the event creator, and an organization Owner,
Manager or Representative. Everyone else is a Member: Staff, Speaker, Judge, Mentor, Volunteer,
Participant, ticket holder. Moderator is **never** automatic.

`ChatHostRoles` (≥ Manager) is deliberately a separate array from `ModeratorRoles` (≥ Staff), derived from
the same `LevelFor` so they cannot disagree. Do not merge them: chat borrowed `ModeratorRoles` before
D-300, which is precisely how an operational seat came to confer the power to ban attendees.

Seeding is idempotent: `AddMemberAsync` finds an existing row and upgrades the role rather than inserting, and posts a join message **only for a genuinely new member** — so an unpublish/republish cycle produces neither duplicate members nor a second "X joined".

### Staff lifecycle

**SHIPPED (Phase 2A).** Chat access is granted on **acceptance**, not invitation — an invite the user never answered must not put them in the room.

- `RespondAsync(accept: true)` -> `AddMemberByEventAsync(..., "Member")` (D-300). It was `"Host"`: accepting
  a volunteer or media-team role silently conferred the power to ban attendees and delete anyone's messages.
- Accepting while the event is still a draft has no room to join, so publish also runs
  `AddAcceptedStaffAsMembersAsync` and those acceptances take effect there.
- `RemoveAsync` -> `RemoveStaffMemberAsync`, but only when no *other* accepted assignment still justifies access (a user may hold several roles on one event).

`RemoveStaffMemberAsync` encodes the one rule worth stating: **anyone who holds `Host` by standing on the event keeps it**, because they did not hold it by the assignment. Standing is `IEventAuthority`'s answer, not the roster's (D-272): the event's owner qualifies by owning it — no seat required — and so does any **Manager-level** seat (Owner/Manager/Representative). Anyone else is demoted to `Member`, then dropped by `RemoveMemberIfNoTicketsAsync` if they hold no ticket.

> The bar is `EventAuthorityLevel.Manager`, not `Staff`. D-300 lowered a Staff seat to Member, so it must
> no longer *protect* a Host either — otherwise revoking an assignment from someone holding a Staff seat
> would leave a Host row this model says cannot exist. This paragraph said `Staff`+ until 2026-08-08.

### Participant membership lifecycle

**SHIPPED:** join on ticket issuance, for authenticated users only. Guests are excluded because `ChatMember.UserId` is non-nullable (recorded at `DECISIONS.md:332`). Duplicates are prevented by unique `(RoomId, UserId)` plus the existence check in `AddMemberAsync`; a lost race against that index converges on the winner's row rather than failing the caller.

**SHIPPED (Phase 2A):** `RemoveMemberIfNoTicketsAsync` is now called from refund and ticket transfer. It re-checks ticket state on every call, so it can never evict a member who still holds a valid ticket — which is what makes it safe to replay.

### Refund lifecycle

**SHIPPED (Phase 2A).** `RefundService` voids the order's tickets, then calls `RemoveMemberIfNoTicketsAsync`. The cleanup also runs on the `already_refunded` replay path, so a first attempt that reversed the books and then failed before the cleanup still converges on a retry.

**Ticket transfer (Phase 2A):** `ClaimAsync` adds the buyer as `Member` and runs the cleanup for the seller, who is dropped only if this was their last ticket for the event. A replayed claim is refused by the existing `transfer_not_pending` guard and leaves membership untouched.

### Message identity and ordering

**FROZEN → shipped (Phase 1). The contract that must not change after clients ship.**

- **`ChatMessage.Id` is UUIDv7**, via `Guid.CreateVersion7()`. Precedent is already ratified in this repo for new auth tables (`DECISIONS.md:941`), superseding D-006's v4 there. A v7 id is k-sortable, giving a stable total order and a natural keyset cursor.
- **`ClientMessageId`** — a client-supplied `Guid`, unique per room. Delivers idempotent sends, optimistic-UI reconciliation, and offline duplicate suppression from one column. Mirrors the existing `OutboxMessage.IdempotencyKey` pattern (`AuthIdentity.cs:135`).
- **`LastReadMessageId` replaces `LastReadAt`** for unread computation. The old `MarkReadBody(DateTime At)` accepted a client-supplied timestamp written through unclamped — untrustworthy, and unmergeable across devices with skewed clocks. The pointer is now server-validated and moves forward only. A message id is server-assigned, monotonic, and merges across devices by `max`. `LastReadAt` is **retained for analytics only** and is never used to compute unread.

The pre-existing ordering defect this closed: messages ordered by `CreatedAt` alone with a strict `<` pivot, so same-timestamp messages were skipped or duplicated across pages. Ordering is now the `(CreatedAt, Id)` pair — see the **Database schema** section below for why the shipped index differs from the one D-104 named.

### Realtime

**SHIPPED:** `ChatService` writes, then broadcasts through `IRealtimeBroadcaster` (`ChatService.cs:147`). `ChatHub.SendMessage` delegates to the service and does not broadcast (`ChatHub.cs:37-39`) — no double-send.

`IRealtimeBroadcaster` is already a shared realtime platform, not a chat abstraction: nine methods spanning scan, sales, chat, notifications, unread counts, badges, wallet, analytics, and login status. **It requires no restructuring** for announcements, counters, polls, leaderboards, or alerts — those add methods, not architecture.

### Attachments

**SHIPPED** — backend D-110, Flutter D-111, Web D-112.

Attachments are metadata rows on a `ChatMessage` (`chat_attachments`); bytes live in `IStorage`. Flow is presign → PUT to the returned URL → confirm → send with `attachmentIds`. **All authoritative validation runs on confirm** — the first moment the server sees the object: allow-list content type, extension agreement, magic bytes, size, ownership, and image dimensions via ImageSharp.

`ChatMessageKind` remains `{ Text, System }`: an attachment does not create a new message kind, which is why the reserved `attachments: []` array (D-104) was the right shape — filling it in broke nothing.

Detail: the **Database schema** section below for the table, the **Security model — detail** section below for validation and the malware-scanning posture.

### Notifications

**SHIPPED (Phase 2C, D-107):** chat uses the platform's single notification pipeline, `INotificationService.NotifyAsync` — the same one announcements, reminders and approvals use. One call writes the in-app row, broadcasts over SignalR, refreshes the unread badge, and fans out to every active device via `IPushSender` (`FirebasePushSender` when `PUSH_PROVIDER=firebase`), deactivating tokens FCM rejects. There is **no second notification service** and no chat code path touches a push provider directly.

**Fan-out runs off the send path.** `SendMessageAsync` commits and broadcasts the message, then enqueues `ChatNotificationJob` on Hangfire. That ordering is what guarantees a push failure can neither delay the sender nor affect chat persistence.

| Kind | Trigger | Recipients |
|---|---|---|
| `chat_message` | New message | Eligible members who are offline |
| `chat_mention` | `@handle` matching a member | The mentioned member, always (even if online) |
| `chat_announcement` | Message from a Host | Eligible members who are offline |
| `chat_room_locked` | Room transitions to Locked | All eligible members, once |
| `chat_muted` / `chat_banned` / `chat_unbanned` | Moderation action | The affected member only |

Eligibility is one SQL query: never the sender, never a banned member, never an actively-muted member. "Offline" is inferred from the D-106 connection registry — a member with a live connection already got the message over SignalR. Payload is `{ notificationType: "chat", eventId, roomId, messageId, senderId }`, enough for the Event → Room → Message deep link. Bodies are truncated to a 120-character preview.

**Still blocked client-side:** mobile has no `firebase_messaging` dependency, so no push reaches a handset yet. The server half is complete. Preferences and quiet hours do not exist anywhere in Kurx — see D-107.

Announcements are a **separate, complete** system — `AnnouncementService` fans out over push/email/WhatsApp (`:170-220`) with audience targeting (`:124-125`). It has no chat integration.

**DEFERRED:** batching, grouping, quiet hours, mention notifications. All additive server-side; none affect the frozen contract.

### E2EE readiness

**DEFERRED**, but the foundation is unusually strong and is documented in the **Security model — detail** section below. One constraint binds Phase 1: **server-side full-text message search must not enter the v1 API contract**, because promising it makes E2EE a breaking change later. Everything else about E2EE remains additive.

### Known conflicts with older documentation

Corrected 2026-07-18 as part of this freeze: the roadmap previously recorded Phase C as complete with reactions, typing indicators, read receipts, and promote/demote/kick — none of which exist in code. See D-104 for the full list.

### Lifecycle

Chat state is derived from event state and is one-way: `Active → Locked → Archived` (D-122 – D-124).
The rule lives in `Kurx.Domain/Entities/ChatLifecycle.cs` and is the only place it is computed.
Full detail: §6a above.

---

## Web client


Architecture: the **Backend architecture — detail** section below. API: the **REST contract** section below. Realtime: the **Realtime — SignalR** section below.

**Status: SHIPPED (Phase 3B, D-109).** Routes `/chats` and **`/chats/[roomId]`**, backed by server actions over the frozen contract, with SignalR live updates.

> ⚠️ **Updated 2026-08-08.** Three claims below had gone stale and are corrected in place: the route was
> `[eventId]` until D-292 re-keyed navigation on room identity (a DM has no event, so a room id is the only
> identifier both kinds share); the "out of scope" list had been overtaken by D-110/D-114/D-293/D-295; and
> moderation is no longer Host-only (D-301). The rest of this page — the attachments pipeline, presence,
> and lifecycle — was verified against the code and is current.

### What was already available

`@microsoft/signalr` was already a dependency, so chat introduced no new realtime client. The two pre-existing usages were **not** usable precedents for an authenticated hub, however:

- `components/auth/login-waiting.tsx` → `/hubs/login`, which is **anonymous by design** (AM9); authorization is the poll token, not a session.
- `components/host/live-sales-status.tsx` → `/hubs/sales` with **no token**, and it never invokes `JoinOrg` — it renders the connection state as a badge and would fail authorization if it tried to join a group.

Chat is therefore the first authenticated hub connection on web. See "Realtime authentication" below.

### Parity requirement

Web and Flutter expose the same chat experience. Parity is enforced structurally: the ordering, de-duplication, cursor, optimistic-reconciliation and post-blocked rules live in one framework-free module per client (`lib/chat-merge.ts` on web, `chat_cache.dart`/`chat_repository_impl.dart` on Flutter), written to mirror each other one-for-one.

Neither client may re-derive permissions. Both render from the server-computed `capabilities` object (the **REST contract** section below); this is what keeps them in parity as future chat modes ship server-side.

### Shipped scope

Matching the backend surface — nothing beyond it:

- Room view, addressed by **room** (`GET /v1/chat/rooms/{roomId}`) — event rooms and DMs enter through the same door (D-292)
- Message history with cursor pagination and infinite scroll
- Send, **reply**, edit, delete-for-me, unsend
- Reactions, forward, link previews, shared media
- Message pin with duration, **and** conversation pin — two different things (D-296)
- Mute and archive a conversation, including event rooms (D-306)
- Search across every room the caller is in
- Delivery and read receipts — three-state ticks
- Unread badge, driven by `LastReadMessageId`
- Moderation UI — mute, ban, pin, room policy, **promote/demote moderator** — gated entirely on `capabilities`
- Report a message
- `GET /v1/me/chats` list, with `?archived=`

> **The "out of scope" list that stood here is gone.** It named typing indicators, presence, reactions,
> threads, edit, forward, attachments and search as unimplemented backend-side. All except threads have
> since shipped (D-110, D-114, D-293, D-295) and all are wired on web. Threads remain absent by design —
> replies quote a message, they do not open a sub-conversation.

**Permissions are the server's answer, never re-derived.** Moderation is `canModerate` (Moderator **or**
Host, D-301); promote/demote is `canManageModerators` and room settings are `canManageRoom`, both
**Host-only** and deliberately not implied by `canModerate` — a Moderator moderates people, never the room.

### Offline expectations

The web client is not required to work offline. It must still:

- Send with a `clientMessageId` so retries are idempotent and consistent with Flutter.
- Reconcile after reconnect using `?after=<cursor>`, driven by the `id` in the realtime envelope.

These are the same two obligations Flutter carries; sharing them keeps one server contract rather than two.

### Realtime authentication

`/hubs/chat` is `[Authorize]` and a browser WebSocket cannot send an Authorization header, so the token must ride `?access_token=`. The web session token is httpOnly. `GET /api/realtime-token` returns it to same-origin client JS **for the socket handshake only** — REST stays in server actions. The security tradeoff is recorded in full in D-109.

### Desktop UX

Two-pane layout with a persistent room sidebar; type-to-filter; ↑/↓/Enter to switch rooms; Enter to send, Shift+Enter for a newline; right-click for moderation; Esc to dismiss. Visually different from Flutter, behaviourally identical.

### Attachments (Phase 4C, D-112)

Pipeline: pick / drop / paste → `presignAttachmentAction` → **browser-direct `XMLHttpRequest` PUT** → `confirmAttachmentAction` → send with `attachmentIds`.

The PUT is the one call that does not go through a server action. It uses XHR rather than `fetch` because `fetch` has no upload-progress event, and it sets `withCredentials = false` with no interceptor — a presigned URL carries its own signature, and cookies or an `Authorization` header alongside it can invalidate it on real object storage.

- **Three sources, one pipeline**: file picker, drag & drop (depth-counted overlay), clipboard paste. Paste needed no backend change.
- **Sequential queue** — a composer is not a bulk uploader.
- **Send is blocked** while anything is queued/uploading/confirming, so a message never becomes permanent referencing an unconfirmed attachment. An empty body with a file is valid.
- **Fresh signed URL per download**, never cached; filenames always from `ChatAttachmentView.fileName`.
- **`next/image` is bypassed** for attachments — signed short-lived URLs cannot be optimized; `loading="lazy"` + `decoding="async"` are used instead.

### Presence (Phase 5C, D-119)

Presence is realtime-only and never persisted — nothing about it survives a refresh, because a cached
"online" is a lie the moment the tab closes. All of it lives in one `PresenceState` inside
`useChatRoom`; `lib/chat-presence.ts` holds the reducers and the derived selectors.

- **The roster is seeded once** from `GET /v1/events/{eventId}/chat` (`onlineUserIds`), then
  maintained purely by `PresenceChanged`. Presence is never inferred from a message arriving, and
  nothing polls.
- **Presence events never trigger a refetch.** They are applied locally, ahead of the dispatcher's
  self-healing `refresh()` branch — otherwise every keystroke in the room would cost two requests per
  participant. Unknown event types still fall through to that branch.
- **`presenceEnabled: false` means "we cannot know", not "nobody is here"**: with no shared presence
  store on the server the indicators are absent entirely rather than shown as offline, and typing is
  not reported.
- **Typing expires on the receiver** using the server's `ttlSeconds`; outgoing typing is debounced to
  one `started` and one `stopped` per burst, and is stopped on send and on unmount.
- **Read pointers only move forward** — UUIDv7 ids compare lexically as they do chronologically. One
  room-level pointer drives every bubble; there is no per-message read state.
- **Losing the socket clears online and typing but keeps read receipts**; reconnect re-reads the
  roster rather than restoring what was held during the gap.
- **A presence update does not repaint the list.** `MessageBubble` is memoised and takes presence as
  three primitives, and the reducers return the same object when nothing changed, so React skips both
  the bubbles and the render.

### Tests

`npx vitest run` — **350 tests across 23 files** as of 2026-08-08 (this said "40 tests" from Phase 3B).
Chat's own share spans `chat-logic`, `chat-components`, `chat-features`, `chat-media`, `chat-attachments`,
`chat-presence`, `chat-presence-room` and `chat-upload`. Vitest + React Testing Library only, by
instruction.

Quote a measured run, not this line — and note the suite covers **models and logic, not composer
interaction**: Reply, for instance, is verified by typecheck and by hand, not by a test.

### Lifecycle states (D-122 – D-124)

A room is `Active` while its event runs, `Locked` (read-only) the moment the event ends, and
`Archived` seven days later. One direction only — nothing reopens an archived room.

The client renders from the server-computed `capabilities` and the room's `status`, and never works
the state out from dates itself. Two refusals are distinguished because they mean different things to
a reader: `room_locked` ("this event has ended") and `room_archived` ("history only"). An unknown
future status simply disables the composer, because `canPost` is authoritative.

In an archived room there is no presence, no typing and no notification — only history, which stays
readable. See §6a above.

---

## Flutter client — offline and cache


Architecture: the **Backend architecture — detail** section below. API: the **REST contract** section below.

**Status: SHIPPED (Phase 3A, D-108).** The Flutter client implements this contract. What follows describes the built behaviour.

### Implementation

`features/social/` now has full `data/` and `domain/` layers alongside `presentation/`. `ChatRepositoryImpl` is cache-first, `ChatCache` stores messages/outbox/read-pointer in the existing `kurx_cache` Hive box, `ChatHubClient` wraps `/hubs/chat`, and `ChatRoomController` (Riverpod) drives history, pagination, optimistic send and reconnect catch-up. Verified by `test/features/social/` — **98 tests** as of 2026-08-08 (this said 37, from the phase that introduced the layer).

> **The cache does not survive a session (D-308).** `kurx_cache` keys are scoped to a *room*, not a user —
> `chat_rooms`, `chat_msgs_{roomId}`, `chat_outbox_{roomId}`, `chat_read_{roomId}` — so until 2026-08-08 the
> next person to sign in on the device read the previous user's rooms and messages, and opening a room
> flushed **their** outbox through the shared Dio, delivering a queued message under the wrong account.
> `SessionController.logout()` now clears the whole box, awaited and *before* the auth state flips, with a
> backstop at start-up when no session exists. See D-308 for why the backstop is not on the login path.

Route is `/chats/:eventId` — rooms are addressable only as `GET /v1/events/{eventId}/chat`.

#### Previously (for context)

`mobile/lib/features/social/presentation/pages/` held two static mockups:

- `chat_room_page.dart` — `_messages` is a hardcoded list seeded with one literal string (`:20-22`); `onSend` appends to local `setState` (`:81-83`). No repository, no HTTP, no persistence.
- `my_chats_page.dart` — both tabs render `_ChatList(items: const [])` (`:57`, `:66`), so the empty state always shows; `onTap` is a no-op (`:155`).

The route was `/chats/:roomId`, which could never have loaded a room. The "Direct" tab contradicted the product rule that chat belongs to an event; both were corrected in Phase 3A.

> Earlier revisions of this document, and D-104/D-107, stated that `pubspec.yaml` had no SignalR client and no `firebase_messaging`. The auth workstream has since added `signalr_netcore`, `firebase_core` and `firebase_messaging`, so Phase 3A required **no new dependencies**.

### Existing local storage

Hive is initialised in `main.dart:12-13` with a single non-secret box, `kurx_cache`, used today by `event_cache.dart`, bookmarks, and the theme controller. Secrets live separately in `flutter_secure_storage` via `token_store.dart` — messages are non-secret and belong in Hive, never in secure storage.

### The four backend guarantees this contract depends on

Offline sync is expressible only because Phase 1 freezes these. All four are documented in the **REST contract** section below.

1. **UUIDv7 message ids** — k-sortable, so the local cache can order messages without trusting any clock.
2. **`after` cursor** — the delta primitive. A reconnecting client fetches only what it missed.
3. **`ClientMessageId` idempotency** — a retried send returns the original message instead of creating a duplicate, which is what makes a retry queue safe.
4. **`LastReadMessageId`** — merges across devices by `max`, with no clock-skew failure mode.

### Client contract

#### Local store

One Hive box per concern, inside the existing `kurx_cache` pattern:

- **messages** — keyed by `roomId`, ordered by UUIDv7 id. Ordering is derived from the id, never from `createdAt`.
- **outbox** — pending sends, each stamped with a client-generated `clientMessageId` at compose time, **before** the first network attempt.
- **cursor** — the newest message id successfully synced per room.

#### Send path

1. Generate `clientMessageId`; write the message to the local store as pending; render it immediately (optimistic UI).
2. Enqueue in the outbox.
3. On success, reconcile the local row against the server row by `clientMessageId` and replace the pending id with the server's UUIDv7.
4. On failure, retry with backoff. Retrying is safe **only** because the send is idempotent — the same `clientMessageId` never creates a second message.

#### Reconnect / delta sync

1. Read the stored cursor for the room.
2. `GET /v1/chat/rooms/{roomId}/messages?after=<cursor>` until the page is short.
3. Apply the realtime envelope stream from the hub; its `id` field lets the client detect a gap and fall back to step 2 (see the **Realtime — SignalR** section below).
4. Flush the outbox.

#### Conflict resolution

Messages are append-only and immutable, so there is no write-write conflict to resolve. The only reconcilable states are:

- **Duplicate** — suppressed server-side by unique `(RoomId, ClientMessageId)` and client-side by matching on the same key.
- **Read position** — `max(local, remote)` of `LastReadMessageId`, which is monotonic and therefore commutative across devices.

#### Cross-device synchronisation

Read state syncs through `LastReadMessageId`. Drafts, per-room mute, and pinned state are **DEFERRED** — none affect the frozen contract and all can be added additively.

### Attachments (Phase 4B, D-111)

Upload follows the backend flow exactly: pick → presign → PUT to the returned URL → confirm → send the message referencing the attachment id. Two dependencies carry it — `file_picker` (images and documents via the platform picker, so no permission handler) and `url_launcher` (opens the signed URL through the OS, so nothing is written to device storage).

- **Pending uploads live outside the message list**, above the composer. No permanent message exists until both the upload and the send are confirmed.
- **A queued message waits for its attachments**: `flushOutbox` skips any message whose attachments are not all confirmed, because a pending attachment has no server id.
- **Attachment upload status is persisted** alongside the outbox. Without it, a pending attachment read back after a restart would look confirmed and be sent referencing an id the server never issued.
- **Signed URLs are never cached** — fetched at the moment of use, since the signature expires and membership is re-checked on every mint.
- **Filenames come from `ChatAttachmentView.fileName`**, never from the URL: the backend serves `application/octet-stream` with no `Content-Disposition` by design (D-110).

### Presence (Phase 5B, D-118)

Presence is realtime-only and never persisted: online status, typing and read receipts are all
discarded on restart and re-synchronised from the server. Nothing about presence is cached, because a
cached "online" is a lie the moment the app is closed.

- **The roster is seeded once** from `GET /v1/events/{eventId}/chat` (`onlineUserIds`), then
  maintained purely by `PresenceChanged`. Presence is never inferred from a message arriving, and
  nothing polls.
- **`presenceEnabled: false` means "we cannot know", not "nobody is here."** With no shared presence
  store on the server the indicators are hidden entirely rather than shown as offline.
- **Typing expires on the receiver** using the server's `ttlSeconds`, so a lost stop event cannot
  strand an indicator. Outgoing typing is debounced to one `started` and one `stopped` per burst.
- **Read pointers only move forward** — UUIDv7 ids compare lexically as they do chronologically, so a
  stale or replayed receipt is discarded. One room-level pointer settles every bubble.
- **Losing the socket clears online and typing but keeps read receipts**: stale dots would be
  fabricated presence, whereas already-read stays true. Reconnect re-reads the roster rather than
  restoring what was held during the gap.
- **A heartbeat every 45s** refreshes the server's 120s TTL. This is not polling — presence state
  only ever arrives pushed.

### Deferred

Offline attachments; background sync while the app is terminated (messages arrive as push and the room catches up on open); an outbox flush triggered by a connectivity-restored signal (`connectivity_plus` is not a dependency — the outbox flushes on room open and on send). Push itself is **shipped**: `PushService` registers the FCM token and `PushBootstrap` deep-links a tapped chat notification to `/chats/{eventId}` (D-108).

### Lifecycle states (D-122 – D-124)

A room is `Active` while its event runs, `Locked` (read-only) the moment the event ends, and
`Archived` seven days later. One direction only — nothing reopens an archived room.

The client renders from the server-computed `capabilities` and the room's `status`, and never works
the state out from dates itself. Two refusals are distinguished because they mean different things to
a reader: `room_locked` ("this event has ended") and `room_archived` ("history only"). An unknown
future status simply disables the composer, because `canPost` is authoritative.

In an archived room there is no presence, no typing and no notification — only history, which stays
readable. See §6a above.
