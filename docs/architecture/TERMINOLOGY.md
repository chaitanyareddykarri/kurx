# Kurx — Canonical Terminology

One architectural vocabulary for the whole repository (D-271). If a word here conflicts with a word in
code, a comment, a diagram or a doc, **this file wins** and the other is drift — report it.

> Kurx has **Users**, **Events** and **Representations**.
> It does **not** have organization accounts, organizer accounts, or personal organizations.

---

## 1. Legacy → canonical

| ❌ Legacy concept | ✅ Canonical concept | Notes |
|---|---|---|
| Organization owns Event | **User owns Event** | `events.created_by`. D-268. |
| Organization creates / manages Event | **User creates it; an organization is represented by it** | Managing is authority, not ownership — D-269. |
| Organization-first · Organization-centric | **User-first · Event-first** | D-267. |
| Workspace → Organization → Event | **User Workspace → Event** | D-267. |
| Organization Dashboard · Organization Home · Organization Workspace · Organizer Dashboard | **User Workspace** | The org dashboard is deleted. D-267. `/workspace` must never become one. |
| Workspace (ambiguous) | **User Workspace** *or* **Event Host Workspace** — always say which | Two different surfaces. D-305. See below. |
| Workspace → Create Event · Create Event in primary nav | **Profile → Create Event** | D-305 moved the entry point and put a gate in front of the form. |
| Public / Private as a form field | **`EventProduct`, derived from the Type's `ProductClass`** | The gate *filters* Types; it never overrides the derivation. D-266 M1 + D-305. |
| Public / Private confused with Listed / Unlisted / InviteOnly | **`EventProduct` ≠ `EventVisibility`** | Two separate concepts; neither is merged or renamed. D-305. |
| My Organizations | **Representing** | Representation details, not a navigation hub. D-267. |
| Organization Events (as a container) | **Hosted Events** (a status view over your own events) | Status filters, never containers. D-267. |
| Organization Account · Organizer Account · Organization Owner | **User** + a **seat** in an organization (`OrgRole`) | No account types exist. D-074. |
| Personal Organization · PersonalOrg · `GetOrCreatePersonalOrgAsync` | **Representing = Personal** | The FK-satisfying row is persistence, never domain. D-268. |
| Organizer (as one field) · a person's name shown as the **Representing organization** | **Three separate terms: Creator · Representing organization · Representative** | The *Creator* is the User who owns it (`events.created_by`); the *Representing organization* is the institution answerable for it; the *Representative* is the human who signed its authorization letter. The admin console printed the creator's name under "Representing organization" because it carried no creator field, so a legacy self-representation row rendered a person as an institution — the one thing a reviewer must not be shown. D-381. |
| A legacy `IsPersonal` row displayed as if it were a real organization | **"Legacy personal representation"** — named, never dressed up | Back-filling those events with a real organization to make a screen look right forges the institutional consent D-379 exists to require. Report the data as it is and mark it historical. D-381. |
| Current Organization · `CurrentOrg` · `kurx_org` cookie · org switcher | *(nothing — deleted)* | An event resolves its own org. D-267. |
| `Event.OrgId` | **`Event.RepresentingOrgId`** | The event *represents* an org; it is owned by a user. D-273a. |
| `org_id` on an event payload | **`representing_org_id`** | Same rename on the wire; `org_id` is a deprecated alias. D-273a. |
| `CanManage(isAdmin, role)` · per-service `RoleAsync` | **`IEventAuthority.ResolveAsync`** | One service, one level ladder. D-269. |
| Capability used as a permission | **Capability = what an event supports** | Authority = who may act. They never mix. D-269. |
| Kind · `KindSlug` · `KindCapabilityDefault` · `/v1/kinds/{slug}/capabilities` | **Archetype** · `ArchetypeSlug` · `ArchetypeCapabilityDefault` · `/v1/archetypes/{slug}/capabilities` | D-266 M2. `/v1/kinds` (the discovery registry) is a *different*, still-current route. |

## 2. Words that look legacy but are correct

| Term | Why it stays |
|---|---|
| **organizer** (lowercase, as a description) | A person running an event. Fine in product copy. What is retired is "Organizer **Account**" as an account *type*. |
| `OrgRole` · `OrgMember` · `memberships` | Organization RBAC — 3–5 administrators, not a member directory. Real and current (D-015). |
| `/v1/orgs/{orgId}/…` management sub-resources | The `orgId` is derived from the event, never chosen by a user. Retained with reason (D-269). |
| `/v1/kinds` | The V3 Kind **discovery** registry (filters/quick-browse). Distinct from the retired Kind *capability* axis. |
| `Organization`, `organizations` | Institutions a user may represent. The entity is current; only "account/owner" framing is retired. |
| `IsPersonal` on `/v1/admin/orgs` | Admin-only. Staff must tell a self-representation row from an institution awaiting verification. |

## 3. The two systems that must never merge

| | Decides | Never does |
|---|---|---|
| **`IEventAuthority`** (D-269/D-272) | **WHO** may act on an event — management *and* audience | capability resolution |
| **`ICapabilityService`** (D-266 M2) | **WHAT** an event supports | authorization |

A capability is not a permission. A permission is not a capability. Neither calls the other.

## 4. Authority ladder (the only one)

```
None(0) < Participant(1) < Staff(2) < Manager(3) < Admin(4)
```

`Manager` is reached three equal ways: the event's **creator** (no membership needed), a
**Representative**, or an **Owner/Manager** seat in the represented organization. `Finance` is `None` on
events — money authority over an organization is not operational authority over its events.

`Participant` is reached two equal ways (D-272): an accepted **programme participation** (speaker, judge,
mentor, volunteer) or a **live ticket**. Both mean "part of this event, with no authority over it".

**An organization membership is never, by itself, event access.** Every seat is mapped through
`EventAuthority.LevelFor` and `Finance` maps to `None` — on audience surfaces exactly as on management ones.

### Two permission families

| Family | Permissions | Floor |
|---|---|---|
| **Management** — may this caller run the event | `ViewAttendees`, `ViewAnalytics`, `ManageContent`, `ManageLifecycle`, `Delete` | `Staff`+ |
| **Audience** — does this caller belong to the event (D-272) | `Participate`, `ModerateAudience` | `Participant`+ |

Never gate an audience surface on a management permission: a ticket holder posting to an event feed is not
a manager, and requiring one is a silent lockout. That mistake is what D-272 fixed.

## 5. Naming debt (named, not hidden)

| Name | Status | Why it stays |
|---|---|---|
| `Event.RepresentingOrgId` | **Correct (D-273a)** | Renamed from `OrgId`. Stores the organization the event *represents*; the owner is `CreatedBy`. Never authorize on it directly — use `IEventAuthority`. |
| `events."OrgId"` (the **column**) | **Legacy but acceptable** | The property was renamed without a schema change (D-273a), so the column, its two indexes and its FK keep their original physical names. Deliberate: it lets the application roll back independently of the database. Internal storage detail — it appears nowhere on the wire. |
| `org_id` in an **event** response | **Deprecated compatibility (D-273a)** | Expand-and-contract alias for `representing_org_id`, emitted so clients deployed before the rename keep working. Drop in the contract phase. |
| `Event.OrgId` nullability | **Technical debt → D-273b** | Still non-null. The money spine (`LedgerEntry`, `OrganizationWallet`, `PayoutSchedule`) is keyed non-nullably on an org, so a null representation would leave a captured payment with nowhere to settle. Needs a payee abstraction first. |
| `Organization.IsPersonal` | **Legacy but acceptable** | Marks the self-representation persistence row. Server-filtered out of every user-facing list. |
| `OrgEventRow` / `orgEventRowSchema` | **Legacy but acceptable** | An organizer's event row; the admin console's per-org list still needs it. |
| `/representing/:orgId` routes | **Correct** | The id *is* the organization's, and those surfaces (verification, payouts) are genuinely about the organization. |
| `ApprovalService.RoleAsync` | **Correct** | An approval step names an exact `OrgRole`; the authority ladder deliberately cannot express that. |
| `OrgService` / `OrgInvitationService` / `OrgVerificationService` `RoleAsync` | **Correct** | Organization RBAC with Owner-only rules — a different policy domain from event authority. |
| `AnalyticsService.HasAccess` · `RefundService.HasFinancialAccessForOrderAsync` · `WalletService.HasFinancialAccess` | **Correct** | The financial bar is `Owner`-or-`Finance` — the *inverse* of the event ladder, which puts `Finance` at `None`. Routing it through `IEventAuthority` would lock Finance out of the money it owns. |
| `GateEntryService.ScanAsync` | **Technical debt (D-272)** | Authorizes ticket scanning as *any org member or an accepted `EventAssignment`* — the same creator omission and synthetic-membership dependency D-272 removed from the audience surfaces. Reported, not changed: removing `Finance` from gate scanning needs its own decision. |

## The three surfaces (D-305)

These are three different things and the word "workspace" alone is ambiguous. Always name which.

| Surface | Route | Purpose | Who has it |
|---|---|---|---|
| **👤 Profile** | `/profile` | Identity + professional profile. **Owns the Create Event entry point.** | Everyone |
| **📋 User Workspace** | `/workspace` | The user's event **activity** hub: *Participated Events* + *Created / Hosted Events* (approved). | **Everyone, always** — participation fills it, creation is not required |
| **🏗️ Event Host Workspace** | `host/events/{id}` | Management surface for **one** approved event (details, media, tickets, people, check-in, analytics…). | Opened *from* a created event inside User Workspace |

Three rules that follow, and that a reviewer should reject a change for breaking:

1. **`/workspace` is never an organizer dashboard.** It is an activity hub. A user who has never created
   an event still has one.
2. **Participation is independent of creation.** `Discover → Register/Join → User Workspace ▸ Participated`
   involves no creation flow at all.
3. **Event-management surfaces live in the Event Host Workspace**, never promoted into User Workspace.
