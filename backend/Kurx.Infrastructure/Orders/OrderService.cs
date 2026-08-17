using System.Security.Cryptography;
using System.Text.Json;
using Kurx.Application.Abstractions;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;
using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Orders;

/// <summary>
/// Order / group / ticket issuance (docs/DECISIONS.md D-021, D-036, D-049).
/// Free path (D-021): individual/guest and group registration issue tickets immediately.
/// Paid path (M10/D-049): a ticket type with PricePaise > 0 is gated LIVE on the organizer's
/// CanOrganizePaid + the org's IsOrgVerified (M8); it creates a Pending order + gateway order and
/// issues the ticket in ConfirmPaymentAsync when the Razorpay capture webhook fires, writing the
/// Collected ledger entry and updating the wallet (MockPaymentGateway in dev; real adapter deferred).
/// D-036: a free, non-competition, Individual-mode ticket type may be purchased without an
/// account (userId null) — the resulting guest Order/Ticket is addressable only via
/// Order.GuestAccessToken. Competition ticket types (TicketType.IsCompetition) always require
/// an account and route team joining through AcceptGroupInvitationAsync, never the open
/// JoinGroupAsync join-code path.
/// </summary>
public class OrderService(
    KurxDbContext db,
    TokenService tokens,
    IQrCodeGenerator qrGenerator,
    IStorage storage,
    IEmailSender emailSender,
    IWhatsAppLogService waLog,
    IPaymentGateway paymentGateway,
    IAudienceService audience,
    IInventoryService inventory,
    IEventRegistrationService registration,
    ITrustService trust,
    IRealtimeBroadcaster realtime,
    IProfileVisibilityResolver visibility,
    // D-359 — a competition group registration materialises the authoritative Team beside the legacy
    // purchase Group. Team formation itself stays entirely in the Phase-10 subsystem.
    ITeamService teams,
    ILogger<OrderService> log) : IOrderService
{
    /// <summary>V3 §17.1 (Phase 9): the admission's non-money side effect — adding the buyer to the event chat —
    /// is published through the transactional outbox in the SAME transaction as the order, never inline. The
    /// <see cref="OutboxDispatchJob"/> delivers it at-least-once; <see cref="IChatService.AddMemberByEventAsync"/>
    /// is idempotent, so the random key never needs to dedup. Enqueued only when there is an account to add
    /// (guests hold no chat membership).</summary>
    private void EnqueueChatJoin(Guid eventId, Guid? userId)
    {
        if (userId is null) return;
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "event.chat_join",
            PayloadJson = JsonSerializer.Serialize(new { eventId, userId = userId.Value }),
            IdempotencyKey = $"chat_join:{eventId}:{userId}:{Guid.NewGuid():N}",
        });
    }

    /// <summary>D-186: first real caller of BroadcastSaleAsync/BroadcastAnalyticsAsync — previously defined,
    /// never invoked. Called AFTER the order transaction commits (never inside it): a hub outage must never
    /// roll back a paid order. Best-effort — a live dashboard missing one tick is a UX gap, not data loss.</summary>
    private async Task BroadcastSaleAsync(Guid orgId, Guid eventId, long amountPaise, CancellationToken ct)
    {
        try
        {
            var payload = new { event_id = eventId, amount_paise = amountPaise, at = DateTime.UtcNow };
            await realtime.BroadcastSaleAsync(orgId, payload, ct);
            await realtime.BroadcastAnalyticsAsync(orgId, payload, ct);
        }
        catch { /* live dashboard tick only */ }
    }

    /*
     * D-357 — the pricing unit decides what is charged, and it is the ONLY thing that does.
     *
     * `PricePaise` is an amount without a unit until this is applied. The three live combinations:
     *
     *   PerTicket + Individual  →  price × 1            (unchanged)
     *   PerTicket + Group       →  price × groupSize    ("₹500 per participant", team of 4 = ₹2,000)
     *   PerGroup  + Group       →  price × 1            ("₹2,000 per team", whatever the team size)
     *
     * The bug this exists to make impossible is the middle row applied to the last one: charging
     * ₹2,000 × 4 for a ticket whose organiser said ₹2,000 per team. `BillableUnits` is the single
     * expression of that rule, used for the charge, the OrderItem, and the gateway order alike, so
     * they cannot disagree.
     *
     * `PerGroup + Individual` is degenerate — one registration either way — and yields 1, which is
     * the same answer as PerTicket, so it needs no branch of its own.
     */
    private static int BillableUnits(TicketType tt, int? groupSize)
        => tt.RegistrationMode == RegistrationMode.Group && tt.PricingUnit == PricingUnit.PerTicket
            ? groupSize ?? 1
            : 1;

    /// <summary>What a group order costs, in paise. Kept beside <see cref="BillableUnits"/> because the
    /// two are one rule: an amount is the unit count times the unit price, and nothing else.</summary>
    private static long AmountFor(TicketType tt, int? groupSize, long? tierPricePaise = null)
        => (tierPricePaise ?? tt.PricePaise) * BillableUnits(tt, groupSize);

    /*
     * D-366 — which band prices THIS team.
     *
     * A team ticket may carry a set of size bands (2 → ₹250, 3 → ₹300, 4–5 → ₹400) instead of one price
     * for its whole range. Resolution is deliberately strict in both directions:
     *
     *   · no band contains the size          → `no_price_for_team_size`, the registration is refused
     *   · more than one contains it          → `ambiguous_price_rule`, a configuration error
     *
     * Never "take the first match". Two matching bands mean the organiser's intent is genuinely unknown,
     * and charging one of two prices they wrote is worse than refusing: the attendee pays an amount
     * nobody decided. It is also unreachable in a correct database — an exclusion constraint forbids
     * overlap — so this is the code that turns a corrupted configuration into a refusal rather than a
     * silent wrong charge.
     *
     * A ticket with NO bands returns null, and the caller prices from `PricePaise` exactly as before,
     * which is every ticket type that predates D-366.
     */
    private async Task<(long? Price, string? Error)> TierPriceAsync(TicketType tt, int? groupSize, CancellationToken ct)
    {
        if (tt.RegistrationMode != RegistrationMode.Group) return (null, null);
        var tiers = await db.TicketPriceTiers.AsNoTracking()
            .Where(x => x.TicketTypeId == tt.Id).ToListAsync(ct);
        if (tiers.Count == 0) return (null, null);

        var size = groupSize ?? 1;
        var matching = tiers.Where(x => size >= x.MinSize && size <= x.MaxSize).ToList();
        if (matching.Count == 0) return (null, "no_price_for_team_size");
        if (matching.Count > 1) return (null, "ambiguous_price_rule");
        return (matching[0].PricePaise, null);
    }

    /// <summary>Whether joining an already-registered group takes another seat.
    ///
    /// <para><b>`PerGroup` is the whole point:</b> the team slot was bought as one unit, so its members
    /// cost no further inventory and <c>Quantity</c> finally means "number of teams". Under `PerTicket`
    /// each person is a separately-priced seat and still consumes one, which is what every pre-D-357 row
    /// does — so nothing existing changes.</para></summary>
    private static bool JoinConsumesInventory(TicketType tt) => tt.PricingUnit != PricingUnit.PerGroup;

    public async Task<ServiceResult<OrderView>> CreateOrderAsync(Guid? userId, Guid eventId, CreateOrderInput input, CancellationToken ct = default)
    {
        var tt = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == input.TicketTypeId && t.EventId == eventId, ct);
        if (tt is null) return ServiceResult<OrderView>.Fail("not_found");

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null || ev.Status != EventStatus.Published) return ServiceResult<OrderView>.Fail("not_found");

        // Client idempotency (V3 §17.1, Phase 9): a retried create with the same key returns the ORIGINAL order
        // rather than consuming inventory again. SCOPED TO THE CALLER — an authenticated user's own orders, or a
        // guest's own (event, phone) — so one caller's key can never retrieve another caller's order nor disclose a
        // guest access token. Checked before any mutation; the matching per-caller unique index is the backstop.
        if (input.IdempotencyKey is { } idem)
        {
            var callerPhone = userId is null && !string.IsNullOrWhiteSpace(input.GuestPhone)
                ? AuthService.NormalizePhone(input.GuestPhone!) : null;
            var prior = userId is not null
                ? await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.EventId == eventId && o.UserId == userId && o.IdempotencyKey == idem, ct)
                : await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.EventId == eventId && o.UserId == null && o.GuestPhone == callerPhone && o.IdempotencyKey == idem, ct);
            if (prior is not null)
            {
                var priorView = await BuildOrderViewAsync(prior, ct);
                return ServiceResult<OrderView>.Success(prior.UserId is null ? priorView with { GuestAccessToken = prior.GuestAccessToken } : priorView);
            }
        }

        // V3 §4.4 (Phase 5): the audience gate at registration. No rule ⇒ allowed (open), so existing events
        // are unaffected. DENY BY DEFAULT when a rule exists. Every ticket-issuing path routes through the
        // same AudienceDenialReasonAsync → EvaluateAsync, so the decision is identical and nothing bypasses it.
        if (await AudienceDenialReasonAsync(userId, eventId, "order", ct) is { } denied)
            return ServiceResult<OrderView>.Fail(denied);

        var now = DateTime.UtcNow;
        if (now < tt.SaleStarts || now > tt.SaleEnds) return ServiceResult<OrderView>.Fail("not_on_sale");

        var requiresAccount = tt.IsCompetition || tt.PricePaise > 0;

        string? guestName = null, guestPhone = null, guestEmail = null;
        if (userId is null)
        {
            if (requiresAccount) return ServiceResult<OrderView>.Fail("account_required");
            if (tt.RegistrationMode == RegistrationMode.Group) return ServiceResult<OrderView>.Fail("guest_group_not_supported");
            if (string.IsNullOrWhiteSpace(input.GuestName) || string.IsNullOrWhiteSpace(input.GuestPhone))
                return ServiceResult<OrderView>.Fail("guest_contact_required");

            guestName = input.GuestName!.Trim();
            guestPhone = AuthService.NormalizePhone(input.GuestPhone!);
            guestEmail = string.IsNullOrWhiteSpace(input.GuestEmail) ? null : input.GuestEmail!.Trim();
        }

        if (tt.RegistrationMode == RegistrationMode.Group
            && (input.GroupSize is null || input.GroupSize < tt.GroupMin || input.GroupSize > tt.GroupMax))
            return ServiceResult<OrderView>.Fail("invalid_group_size");

        var fields = await db.FormFields.AsNoTracking().Where(f => f.TicketTypeId == tt.Id).ToListAsync(ct);
        var answers = input.Answers ?? new Dictionary<string, string>();
        if (fields.Any(f => f.Required && !answers.ContainsKey(f.Key)))
            return ServiceResult<OrderView>.Fail("missing_required_field");

        // Friendly early-out only; the authoritative check is BuyerLimitReachedAsync inside the
        // transaction below. Kept here so an already-at-limit buyer is refused before the paid path
        // spends a gateway-order call, exactly like the sold-out pre-check.
        var existingCount = userId is not null
            ? await CountUserTicketsAsync(userId.Value, tt.Id, ct)
            : await CountGuestTicketsAsync(guestPhone!, tt.Id, ct);
        if (existingCount >= tt.PerUserLimit) return ServiceResult<OrderView>.Fail("limit_exceeded");

        var regAnswersJson = ToJsonOrNull(fields, answers, FormFieldScope.PerRegistration);
        var participantAnswersJson = ToJsonOrNull(fields, answers, FormFieldScope.PerParticipant);

        var poolId = await inventory.EnsureGeneralPoolAsync(tt.Id, tt.EventId, tt.Quantity, tt.Sold, ct);
        // Authoritative sold-out pre-check (§17.1): the pool, never the legacy Sold mirror. The conditional
        // decrement below is the real guard; this is a friendly early-out that also spares the paid path a
        // wasted gateway-order call when already sold out.
        if (await inventory.AvailableAsync(tt.Id, ct) < 1) return ServiceResult<OrderView>.Fail("sold_out");

        // ── Paid path (M10, D-049): a Pending order + gateway order; a §17.1 HOLD reserves inventory; the
        //    ticket + admission issue on capture. ──
        if (tt.PricePaise > 0)
        {
            // Payments must be enabled for this event, read LIVE (M8): organizer paid-verified + org
            // verified. The event is already confirmed Published above. userId is non-null (paid ⇒ account).
            var caps = await trust.GetUserCapabilitiesAsync(ev.CreatedBy, ct);
            var orgCaps = await trust.GetOrgCapabilitiesAsync(ev.CreatedBy, ev.RepresentingOrgId, ct);
            if (!caps.CanOrganizePaid || !orgCaps.IsOrgVerified)
                return ServiceResult<OrderView>.Fail("payments_not_enabled");

            /*
             * D-357 — `paid_group_not_supported_yet` stood here and refused every non-Individual mode.
             * It is gone because the path below now exists, not because the guard was inconvenient: the
             * amount is unit-derived, the group is materialised at capture, and joining it consumes no
             * further inventory under PerGroup. Removing it without those three would have shipped a
             * team event that charges per head.
             */
            // D-366 — the band the team's SIZE resolves to, or null for a ticket priced by one amount.
            // Refused rather than approximated: a team whose size no band covers must not be charged the
            // headline price by accident.
            var (tierPrice, tierError) = await TierPriceAsync(tt, input.GroupSize, ct);
            if (tierError is not null) return ServiceResult<OrderView>.Fail(tierError);

            var paidAmount = AmountFor(tt, input.GroupSize, tierPrice);
            var paidOrder = new Order
            {
                UserId = userId,
                EventId = eventId,
                TicketTypeId = tt.Id,
                Status = OrderStatus.Pending,
                AmountPaise = paidAmount,
                Currency = ev.SettlementCurrency,   // V3 §9.1 — an event's money is in its settlement currency
                AnswersJson = answers.Count > 0 ? JsonSerializer.Serialize(answers) : null,
                IdempotencyKey = input.IdempotencyKey,
                // Carried to capture: the Group is created there, so both survive as order state rather
                // than being re-asked of a buyer who has already paid.
                GroupSize = tt.RegistrationMode == RegistrationMode.Group ? input.GroupSize : null,
                GroupDisplayName = tt.RegistrationMode == RegistrationMode.Group ? input.DisplayName : null,
            };
            // Order.Id is app-generated, so the gateway order can be created before the transaction (the external
            // call must not run inside an open DB transaction). A rare lost hold race orphans a gateway order only.
            var gatewayOrder = await paymentGateway.CreateOrderAsync(paidOrder.Id, paidAmount, ct);
            paidOrder.RazorpayOrderId = gatewayOrder.GatewayOrderId;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (await BuyerLimitReachedAsync(tt, userId, guestPhone, ct))
                return ServiceResult<OrderView>.Fail("limit_exceeded");
            // Reserve-first (§17.1): conditional HOLD. No capacity ⇒ roll back; nothing was written.
            if (!await inventory.TryHoldManyAsync([new PoolDraw(poolId, 1)], ct))
                return ServiceResult<OrderView>.Fail("sold_out");
            db.Orders.Add(paidOrder);
            // `Qty` is the BILLABLE quantity, so `Qty × UnitPricePaise == AmountPaise` holds for every
            // combination — 1 × ₹2,000 for a team, 4 × ₹500 for four per-participant seats. The team's
            // SIZE lives on `Order.GroupSize`; conflating the two is what made ₹2,000 ambiguous.
            // `UnitPricePaise` is the band that was resolved, not the headline: the order line has to
            // record what this buyer was actually charged, or `Qty × UnitPrice == AmountPaise` stops
            // holding the moment a team pays a band above the cheapest one (D-366).
            db.OrderItems.Add(new OrderItem { OrderId = paidOrder.Id, TicketTypeId = tt.Id, Qty = BillableUnits(tt, input.GroupSize), UnitPricePaise = tierPrice ?? tt.PricePaise, Currency = ev.SettlementCurrency });
            // ExpireSeatHoldsJob (D-029) reclaims the hold if payment never captures within the window.
            db.SeatHolds.Add(new SeatHold { TicketTypeId = tt.Id, PoolId = poolId, OrderId = paidOrder.Id, Qty = 1, Status = SeatHoldStatus.Active, ExpiresAt = now.AddMinutes(10) });
            tt.Sold += 1;   // legacy mirror (held+issued); the pool's Held is now the authority
            await db.SaveChangesAsync(ct);
            await registration.ProjectOrderInTransactionAsync(paidOrder.Id, ct);   // registration(Pending) + VAR; admission at capture
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<OrderView>.Success(await BuildOrderViewAsync(paidOrder, ct));
        }

        var order = new Order
        {
            UserId = userId,
            EventId = eventId,
            TicketTypeId = tt.Id,
            Status = OrderStatus.Paid,
            AmountPaise = 0,
            Currency = ev.SettlementCurrency,   // V3 §9.1
            AnswersJson = regAnswersJson,
            GuestName = guestName,
            GuestPhone = guestPhone,
            GuestEmail = guestEmail,
            GuestAccessToken = userId is null ? GenerateGuestAccessToken() : null,
            IdempotencyKey = input.IdempotencyKey,
            // D-357 — recorded explicitly rather than inferred from `Qty`, which is now the billable
            // quantity. Free events are always billable-1, so the two agree here; a paid team is where
            // they diverge, and the roster cap must read the same field on both paths.
            GroupSize = tt.RegistrationMode == RegistrationMode.Group ? input.GroupSize : null,
            GroupDisplayName = tt.RegistrationMode == RegistrationMode.Group ? input.DisplayName : null,
        };
        // Free: the amount is zero whatever the unit, so `Qty` carries the billable count for symmetry
        // with the paid path — 1 for a team, 1 for an individual. The team's size is `Order.GroupSize`.
        var orderItem = new OrderItem { OrderId = order.Id, TicketTypeId = tt.Id, Qty = BillableUnits(tt, input.GroupSize), UnitPricePaise = 0, Currency = ev.SettlementCurrency };

        Guid? groupIdForTeam = null;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            if (await BuyerLimitReachedAsync(tt, userId, guestPhone, ct))
                return ServiceResult<OrderView>.Fail("limit_exceeded");
            // Conditional CONSUME (§17.1): the individual/leader takes one seat now. No capacity ⇒ roll back.
            if (!await inventory.TryConsumeManyAsync([new PoolDraw(poolId, 1)], ct))
                return ServiceResult<OrderView>.Fail("sold_out");
            db.Orders.Add(order);
            db.OrderItems.Add(orderItem);
            tt.Sold += 1;   // legacy mirror; the pool's Consumed is the authority

            if (tt.RegistrationMode == RegistrationMode.Individual)
            {
                db.Tickets.Add(NewTicket(orderItem.Id, eventId, userId, null, participantAnswersJson));
            }
            else
            {
                // Group mode always requires an authenticated leader — the guest path is already
                // rejected above (guest_group_not_supported), so userId is guaranteed non-null here.
                var leaderId = userId!.Value;
                var groupNumber = await NextGroupNumberAsync(eventId, ct);
                var group = new Group
                {
                    EventId = eventId,
                    TicketTypeId = tt.Id,
                    OrderId = order.Id,
                    GroupNumber = groupNumber,
                    DisplayName = input.DisplayName,
                    JoinCode = await GenerateUniqueJoinCodeAsync(ct),
                    LeaderUserId = leaderId,
                };
                db.Groups.Add(group);
                groupIdForTeam = group.Id;

                var leaderUser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == leaderId, ct);
                var leaderMember = new GroupMember
                {
                    GroupId = group.Id,
                    UserId = leaderId,
                    Name = leaderUser.Name,
                    // Canonical column first (D-089): this is copied onto the roster and shown back to
                    // the organizer, so it has to carry the country code rather than bare digits.
                    Phone = leaderUser.PhoneE164 ?? leaderUser.Phone,
                    AnswersJson = participantAnswersJson,
                    JoinedAt = now,
                };
                db.GroupMembers.Add(leaderMember);

                var leaderTicket = NewTicket(orderItem.Id, eventId, leaderId, leaderMember.Id, participantAnswersJson);
                db.Tickets.Add(leaderTicket);
                leaderMember.TicketId = leaderTicket.Id;
            }

            await db.SaveChangesAsync(ct);
            await registration.ProjectOrderInTransactionAsync(order.Id, ct);   // reg + admission + credential + VAR, authoritative
            EnqueueChatJoin(eventId, userId);                                  // §17.1 side effect via outbox, never inline
            // D-359 — AFTER the projection: the Team links to the Registration the money path just made,
            // which is what `Team.RegistrationId` was reserved for. A no-op unless the ticket type is a
            // competition, so a plain group purchase is unchanged.
            if (groupIdForTeam is { } freeGroupId) await teams.MaterialiseForGroupAsync(freeGroupId, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await BroadcastSaleAsync(ev.RepresentingOrgId, eventId, order.AmountPaise, ct);
        var view = await BuildOrderViewAsync(order, ct);
        return ServiceResult<OrderView>.Success(userId is null ? view with { GuestAccessToken = order.GuestAccessToken } : view);
    }

    public async Task<ServiceResult<OrderView>> ConfirmPaymentAsync(string gatewayOrderId, string gatewayPaymentId, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.RazorpayOrderId == gatewayOrderId, ct);
        if (order is null) return ServiceResult<OrderView>.Fail("not_found");
        // Idempotent: a re-delivered webhook for an already-captured order is a no-op.
        if (order.Status == OrderStatus.Paid) return ServiceResult<OrderView>.Success(await BuildOrderViewAsync(order, ct));
        if (order.Status != OrderStatus.Pending) return ServiceResult<OrderView>.Fail("invalid_order_state");

        var tt = await db.TicketTypes.FirstAsync(t => t.Id == order.TicketTypeId, ct);
        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.Id == order.EventId, ct);
        var orderItem = await db.OrderItems.FirstAsync(oi => oi.OrderId == order.Id, ct);
        var poolId = await inventory.GeneralPoolIdAsync(tt.Id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Duplicate-callback safety (§17.1): atomically claim the Pending→Paid transition. Only one capture wins;
        // a concurrent/re-delivered webhook sees 0 rows and is a no-op, so the hold is never converted twice nor a
        // second ticket issued.
        var claimed = await db.Orders.Where(o => o.Id == order.Id && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Paid), ct);
        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<OrderView>.Success(await BuildOrderViewAsync(order, ct));
        }
        await db.Entry(order).ReloadAsync(ct);   // reflect the claimed Paid status on the tracked entity

        db.Payments.Add(new Payment
        {
            OrderId = order.Id, RazorpayPaymentId = gatewayPaymentId, Method = "razorpay",
            Status = "captured", CapturedAt = DateTime.UtcNow,
        });

        // Convert the hold to a consumption (§17.1): the reserved seat becomes consumed. If the hold already
        // expired before this (late) capture, consume directly so Consumed still matches the admission we issue.
        var hold = await db.SeatHolds.FirstOrDefaultAsync(h => h.OrderId == order.Id && h.Status == SeatHoldStatus.Active, ct);
        if (poolId is { } pid)
        {
            if (hold is not null) { hold.Status = SeatHoldStatus.Consumed; await inventory.ConvertHoldToConsumedAsync(pid, hold.Qty, ct); }
            // Late capture: the hold already expired and released its seat, but the payment succeeded and the buyer
            // gets an admission — so consume UNCONDITIONALLY (§9 honours a paid ticket) to keep Consumed == active
            // admissions. It may push Consumed past Total; new sales stay blocked, so no fresh oversell occurs.
            else
            {
                await inventory.ConsumeUnconditionalAsync(pid, 1, ct);
                // Silent before this: the only correct-but-overbooking path in the system left no trace, so an
                // organiser discovered it at the gate. Warn when it actually breaches capacity — the organiser
                // now owes someone a seat, which is an operational fact, not a code error.
                var pool = await db.InventoryPools.AsNoTracking()
                    .Where(p => p.Id == pid)
                    .Select(p => new { p.Consumed, p.Held, p.Total, p.OversellAllowance }).FirstAsync(ct);
                if (pool.Consumed + pool.Held > pool.Total + pool.OversellAllowance)
                    log.LogWarning(
                        "Late capture on order {OrderId} pushed pool {PoolId} past capacity: consumed {Consumed} + held {Held} > total {Total} + allowance {Allowance}. The ticket is honoured; the event is overbooked by {Over}.",
                        order.Id, pid, pool.Consumed, pool.Held, pool.Total, pool.OversellAllowance,
                        pool.Consumed + pool.Held - pool.Total - pool.OversellAllowance);
            }
        }

        // Issue the ticket. Per-participant answers were stashed on the order.
        var fields = await db.FormFields.AsNoTracking().Where(f => f.TicketTypeId == tt.Id).ToListAsync(ct);
        var participantJson = SplitScope(order.AnswersJson, fields, FormFieldScope.PerParticipant);

        if (tt.RegistrationMode == RegistrationMode.Group && order.UserId is { } paidLeaderId)
        {
            /*
             * D-357 — a PAID group materialises its Group here, at capture, and not at checkout.
             *
             * The free path creates the Group immediately because there is nothing to wait for. A paid
             * order is Pending until the gateway confirms, and a Pending team that could already hand out
             * its join code would let an unpaid buyer recruit members into a registration that may never
             * be paid for. Same shape as the free branch otherwise — deliberately, so the two produce
             * identical rows and the roster, gate and projection code cannot tell them apart.
             */
            var leaderUser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == paidLeaderId, ct);
            var group = new Group
            {
                EventId = order.EventId,
                TicketTypeId = tt.Id,
                OrderId = order.Id,
                GroupNumber = await NextGroupNumberAsync(order.EventId, ct),
                DisplayName = order.GroupDisplayName,
                JoinCode = await GenerateUniqueJoinCodeAsync(ct),
                LeaderUserId = paidLeaderId,
            };
            db.Groups.Add(group);

            var leaderMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = paidLeaderId,
                Name = leaderUser.Name,
                Phone = leaderUser.PhoneE164 ?? leaderUser.Phone,   // canonical column first (D-089)
                AnswersJson = participantJson,
                JoinedAt = DateTime.UtcNow,
            };
            db.GroupMembers.Add(leaderMember);

            var leaderTicket = NewTicket(orderItem.Id, order.EventId, paidLeaderId, leaderMember.Id, participantJson);
            db.Tickets.Add(leaderTicket);
            leaderMember.TicketId = leaderTicket.Id;

            // D-359 — the authoritative Team for a competition entry, made in the same transaction as the
            // Group it mirrors. Saved first so the roster read inside sees the captain's GroupMember row.
            await db.SaveChangesAsync(ct);
            await teams.MaterialiseForGroupAsync(group.Id, ct);
        }
        else
        {
            db.Tickets.Add(NewTicket(orderItem.Id, order.EventId, order.UserId, null, participantJson));
        }

        // Ledger write-path (D-028): the payment is Collected funds for the org; update the cached wallet
        // balance in the SAME transaction as the ledger insert (never SUM the ledger on the hot path).
        var ledger = new LedgerEntry
        {
            OrgId = ev.RepresentingOrgId, EventId = ev.Id, AmountPaise = order.AmountPaise, Currency = ev.SettlementCurrency,
            State = LedgerState.Collected, RefType = "payment", RefId = order.Id,
        };
        db.LedgerEntries.Add(ledger);

        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "system", ActorId = order.UserId,
            Action = "order.payment_captured", Entity = "orders", EntityId = order.Id,
            DetailsJson = $"{{\"amount_paise\":{order.AmountPaise},\"payment_id\":\"{gatewayPaymentId}\"}}",
        });

        await db.SaveChangesAsync(ct);

        // Wallet credit as an IN-SQL increment, never a read-modify-write. Two captures for the same org
        // commit concurrently all the time (one webhook per buyer, any replica); loading the row and
        // assigning `wallet.CollectedPaise += x` makes EF emit `SET "CollectedPaise" = <literal>`, so the
        // second writer overwrites the first's balance with a value computed before it landed. The ledger
        // stayed correct and the cache silently drifted low — org revenue vanishing from their balance.
        // `w.CollectedPaise + amount` is evaluated by Postgres under the row lock, so no update is lost.
        // Runs after the SaveChanges above because LastLedgerEntryId is a real FK to the row it inserts.
        var creditPaise = order.AmountPaise;
        var credited = await db.OrganizationWallets.Where(w => w.OrgId == ev.RepresentingOrgId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.CollectedPaise, w => w.CollectedPaise + creditPaise)
                .SetProperty(w => w.LifetimeEarnedPaise, w => w.LifetimeEarnedPaise + creditPaise)
                .SetProperty(w => w.LastLedgerEntryId, ledger.Id)
                .SetProperty(w => w.UpdatedAt, DateTime.UtcNow), ct);
        // Previously a missing wallet threw out of FirstAsync and rolled the capture back. Keep that:
        // banking a payment whose funds land nowhere is strictly worse than failing the webhook.
        if (credited == 0)
            throw new InvalidOperationException(
                $"No wallet row for org {ev.RepresentingOrgId}; refusing to capture payment for order {order.Id}.");

        await registration.ProjectOrderInTransactionAsync(order.Id, ct);   // admission created now (ticket exists); registration Confirmed
        EnqueueChatJoin(order.EventId, order.UserId);                      // §17.1 side effect via outbox, never inline
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await BroadcastSaleAsync(ev.RepresentingOrgId, order.EventId, order.AmountPaise, ct);
        return ServiceResult<OrderView>.Success(await BuildOrderViewAsync(order, ct));
    }

    public async Task<ServiceResult<GroupMemberView>> JoinGroupAsync(Guid userId, JoinGroupInput input, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.JoinCode == input.JoinCode, ct);
        if (group is null) return ServiceResult<GroupMemberView>.Fail("invalid_join_code");

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == group.EventId, ct);
        if (ev is null || ev.Status != EventStatus.Published) return ServiceResult<GroupMemberView>.Fail("not_found");

        var tt = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == group.TicketTypeId, ct);
        if (tt is null) return ServiceResult<GroupMemberView>.Fail("not_found");
        if (tt.IsCompetition) return ServiceResult<GroupMemberView>.Fail("competition_requires_invitation");

        return await AddMemberToGroupAsync(group, tt, userId, input.DisplayName, input.Answers, ct);
    }

    public async Task<ServiceResult<GroupMemberView>> AcceptGroupInvitationAsync(Guid userId, string inviteToken, IReadOnlyDictionary<string, string>? answers, CancellationToken ct = default)
    {
        var inv = await db.EventInvitations.FirstOrDefaultAsync(i => i.InviteToken == inviteToken && i.GroupId != null, ct);
        if (inv is null || inv.Status == InvitationStatus.Revoked) return ServiceResult<GroupMemberView>.Fail("not_found");
        if (inv.RsvpStatus == InvitationRsvpStatus.Accepted) return ServiceResult<GroupMemberView>.Fail("already_accepted");
        if (inv.RsvpStatus == InvitationRsvpStatus.Declined) return ServiceResult<GroupMemberView>.Fail("invitation_declined");

        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == inv.GroupId, ct);
        if (group is null) return ServiceResult<GroupMemberView>.Fail("not_found");
        var tt = await db.TicketTypes.FirstOrDefaultAsync(t => t.Id == group.TicketTypeId, ct);
        if (tt is null) return ServiceResult<GroupMemberView>.Fail("not_found");

        // If the invite named a specific phone, only that phone's account may accept it — same
        // targeted-redemption rule TicketTransferService.ClaimAsync already enforces on transfer claims.
        if (inv.Phone is not null)
        {
            var accepter = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
            if (accepter.Phone != inv.Phone) return ServiceResult<GroupMemberView>.Fail("phone_mismatch");
        }

        var result = await AddMemberToGroupAsync(group, tt, userId, inv.Name, answers, ct);
        if (!result.Ok) return result;

        inv.RsvpStatus = InvitationRsvpStatus.Accepted;
        inv.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<OrderView>> MyTicketsAsync(Guid userId, int page = 0,
        int pageSize = OrderPaging.MaxPageSize, CancellationToken ct = default)
    {
        // A ticket is listed for its CURRENT holder (Ticket.UserId), never the order's buyer (D-062).
        // TicketTransferService.ClaimAsync reassigns Ticket.UserId and rotates Ticket.Code so the
        // sender's old QR dies; keying tickets off Order.UserId would hand the sender the rotated
        // code anyway (the QR payload is the code) and hide the ticket from the claimant entirely.
        // The buyer still sees the order itself — it is their payment record — just not tickets that
        // are no longer theirs.
        var ticketOrderIds = await (
            from t in db.Tickets.AsNoTracking()
            join oi in db.OrderItems.AsNoTracking() on t.OrderItemId equals oi.Id
            where t.UserId == userId
            select oi.OrderId).Distinct().ToListAsync(ct);

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.UserId == userId || ticketOrderIds.Contains(o.Id))
            // DB-6: a buyer's orders are their purchase record — a row that repeats on one page and
            // vanishes from the next reads as a double charge. Ordering on CreatedAt alone leaves that to
            // the database whenever two orders share a tick.
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip(Math.Max(0, page) * Math.Clamp(pageSize, 1, OrderPaging.MaxPageSize))
            .Take(Math.Clamp(pageSize, 1, OrderPaging.MaxPageSize))
            .ToListAsync(ct);
        if (orders.Count == 0) return [];

        // Batched, not looped. BuildOrderViewAsync costs two queries per order, so calling it once per
        // row made this endpoint 2N+2 round trips — a buyer with 200 orders issued 402 queries for one
        // request. Same data, three queries, regardless of N.
        var orderIds = orders.Select(o => o.Id).ToList();
        var groupByOrder = await db.Groups.AsNoTracking()
            .Where(g => orderIds.Contains(g.OrderId))
            .ToDictionaryAsync(g => g.OrderId, ct);
        var ticketsByOrder = (await (
                from t in db.Tickets.AsNoTracking()
                join oi in db.OrderItems.AsNoTracking() on t.OrderItemId equals oi.Id
                where orderIds.Contains(oi.OrderId) && t.UserId == userId
                select new { oi.OrderId, Ticket = t }).ToListAsync(ct))
            .GroupBy(x => x.OrderId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<Ticket>)x.Select(v => v.Ticket).ToList());

        // Denormalised so the list renders on its own. Without these the "my tickets" surfaces had only
        // the event GUID to display where a title belongs.
        var eventById = await db.Events.AsNoTracking()
            .Where(e => orders.Select(o => o.EventId).Contains(e.Id))
            .Select(e => new { e.Id, e.Title, e.Slug }).ToDictionaryAsync(e => e.Id, ct);
        var ticketTypeNameById = await db.TicketTypes.AsNoTracking()
            .Where(t => orders.Select(o => o.TicketTypeId).Contains(t.Id))
            .Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        return orders
            .Select(o =>
            {
                var ev = eventById.GetValueOrDefault(o.EventId);
                return ToOrderView(o, groupByOrder.GetValueOrDefault(o.Id), ticketsByOrder.GetValueOrDefault(o.Id) ?? [],
                    ev?.Title, ev?.Slug, ticketTypeNameById.GetValueOrDefault(o.TicketTypeId));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<GroupView>> MyGroupsAsync(Guid userId, int page = 0,
        int pageSize = OrderPaging.MaxPageSize, CancellationToken ct = default)
    {
        var size = Math.Clamp(pageSize, 1, OrderPaging.MaxPageSize);
        var memberOf = db.GroupMembers.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.GroupId);
        var leaderOf = db.Groups.AsNoTracking().Where(g => g.LeaderUserId == userId).Select(g => g.Id);
        // Ordered before paging: an unordered Skip/Take is not a stable page in Postgres.
        var groupIds = await memberOf.Union(leaderOf).Distinct().OrderBy(id => id)
            .Skip(Math.Max(0, page) * size).Take(size).ToListAsync(ct);

        if (groupIds.Count == 0) return [];

        // Batched for the same reason as MyTicketsAsync: BuildGroupViewAsync is four queries per group
        // (group, capacity, members, then a visibility resolve + user load), so the loop scaled 4N with
        // group count. Five queries here regardless, and the visibility resolve — the expensive one —
        // now runs ONCE for every member across every group instead of once per group.
        var groups = await db.Groups.AsNoTracking().Where(g => groupIds.Contains(g.Id)).ToListAsync(ct);
        if (groups.Count == 0) return [];

        var capacityByOrder = await db.OrderItems.AsNoTracking()
            .Where(oi => groups.Select(g => g.OrderId).Contains(oi.OrderId))
            .GroupBy(oi => oi.OrderId)
            .Select(x => new { OrderId = x.Key, Qty = x.Max(oi => oi.Qty) })
            .ToDictionaryAsync(x => x.OrderId, x => x.Qty, ct);

        var members = await db.GroupMembers.AsNoTracking()
            .Where(m => groupIds.Contains(m.GroupId)).OrderBy(m => m.JoinedAt).ToListAsync(ct);

        var linkable = await visibility.VisibleProfileIdsAsync(
            members.Where(m => m.UserId != null).Select(m => m.UserId!.Value).Distinct().ToList(), null, ct);
        var users = await db.Users.AsNoTracking().Where(u => linkable.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        var membersByGroup = members.GroupBy(m => m.GroupId).ToDictionary(x => x.Key, x => x.ToList());
        return groups.Select(g => ToGroupView(g, capacityByOrder.GetValueOrDefault(g.OrderId),
            membersByGroup.GetValueOrDefault(g.Id) ?? [], users)).ToList();
    }

    /// <summary>The single GroupView shape, shared by the per-group path and the batched list path.
    /// <paramref name="users"/> holds only profiles the viewer may link to — absence means "not linkable",
    /// so the username/avatar simply stay null, exactly as the per-group path resolves it.</summary>
    private static GroupView ToGroupView(Group group, int capacity, IReadOnlyList<GroupMember> members,
        IReadOnlyDictionary<Guid, User> users) =>
        new(group.Id, group.EventId, group.TicketTypeId, group.GroupNumber, group.DisplayName, group.JoinCode,
            group.LeaderUserId, capacity,
            members.Select(m =>
            {
                var u = m.UserId != null && users.TryGetValue(m.UserId.Value, out var found) ? found : null;
                return new GroupMemberView(m.Id, m.UserId, m.Name, m.Phone, m.TicketId, m.AnswersJson, m.JoinedAt,
                    u?.Username, u?.AvatarKey);
            }).ToList());

    public async Task<ServiceResult<GroupView>> GetGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return ServiceResult<GroupView>.Fail("not_found");

        var isMember = group.LeaderUserId == userId
            || await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
        if (!isMember) return ServiceResult<GroupView>.Fail("not_found"); // hide existence (D-018 pattern)

        var view = await BuildGroupViewAsync(groupId, ct);
        return view is null ? ServiceResult<GroupView>.Fail("not_found") : ServiceResult<GroupView>.Success(view);
    }

    public async Task<ServiceResult<bool>> ResendTicketAsync(Guid userId, Guid ticketCode, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Code == ticketCode, ct);
        if (ticket is null) return ServiceResult<bool>.Fail("not_found");
        if (ticket.UserId != userId) return ServiceResult<bool>.Fail("forbidden");

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == ticket.EventId, ct);
        if (user is null || ev is null) return ServiceResult<bool>.Fail("not_found");

        try
        {
            var qrPng = qrGenerator.GeneratePng(ticket.Code.ToString());
            var storageKey = $"tickets/{ticket.Code:N}.png";
            await storage.PutAsync(storageKey, qrPng, "image/png", ct);

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                var attachment = new EmailAttachment("ticket-qr.png", "image/png", qrPng);
                await emailSender.SendAsync(user.Email, $"Your ticket for {ev.Title}",
                    $"<p>Hi {user.Name},</p><p>Here is your ticket QR code for <strong>{ev.Title}</strong>.</p>",
                    new[] { attachment }, ct);
            }

            var mediaUrl = await storage.PresignGetAsync(storageKey, ttl: null, ct);
            await waLog.SendAndLogAsync(user.PhoneE164 ?? user.Phone, $"Here is your ticket for {ev.Title}: {mediaUrl}",
                WhatsAppMessageKind.TicketDelivery, "ticket", ticket.Id, ct: ct);
        }
        catch
        {
            return ServiceResult<bool>.Fail("send_failed");
        }

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<OrderView>> GetGuestOrderAsync(string accessToken, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.GuestAccessToken == accessToken, ct);
        if (order is null) return ServiceResult<OrderView>.Fail("not_found");
        return ServiceResult<OrderView>.Success(await BuildOrderViewAsync(order, ct));
    }

    public async Task<ServiceResult<bool>> ResendGuestOrderAsync(string accessToken, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.GuestAccessToken == accessToken, ct);
        if (order is null) return ServiceResult<bool>.Fail("not_found");

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == order.EventId, ct);
        if (ev is null) return ServiceResult<bool>.Fail("not_found");

        var tickets = await (
            from t in db.Tickets.AsNoTracking()
            join oi in db.OrderItems.AsNoTracking() on t.OrderItemId equals oi.Id
            where oi.OrderId == order.Id
            select t).ToListAsync(ct);

        try
        {
            foreach (var ticket in tickets)
            {
                var qrPng = qrGenerator.GeneratePng(ticket.Code.ToString());
                var storageKey = $"tickets/{ticket.Code:N}.png";
                await storage.PutAsync(storageKey, qrPng, "image/png", ct);

                if (!string.IsNullOrWhiteSpace(order.GuestEmail))
                {
                    var attachment = new EmailAttachment("ticket-qr.png", "image/png", qrPng);
                    await emailSender.SendAsync(order.GuestEmail, $"Your ticket for {ev.Title}",
                        $"<p>Hi {order.GuestName},</p><p>Here is your ticket QR code for <strong>{ev.Title}</strong>.</p>",
                        new[] { attachment }, ct);
                }

                var mediaUrl = await storage.PresignGetAsync(storageKey, ttl: null, ct);
                await waLog.SendAndLogAsync(order.GuestPhone!, $"Here is your ticket for {ev.Title}: {mediaUrl}",
                    WhatsAppMessageKind.TicketDelivery, "ticket", ticket.Id, ct: ct);
            }
        }
        catch
        {
            return ServiceResult<bool>.Fail("send_failed");
        }

        return ServiceResult<bool>.Success(true);
    }

    /// <summary>The single audience gate (V3 §4.4) shared by every ticket-issuing path — order creation and
    /// both group-join tails — so the eligibility decision is identical everywhere and none can bypass it.
    /// Returns null when allowed, or "not_eligible" after logging the denial.</summary>
    private async Task<string?> AudienceDenialReasonAsync(Guid? userId, Guid eventId, string via, CancellationToken ct)
    {
        var eligibility = await audience.EvaluateAsync(userId, eventId, ct);
        if (eligibility.Allowed) return null;
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = userId is null ? "guest" : "user", ActorId = userId,
            Action = "audience.register_denied", Entity = "events", EntityId = eventId,
            DetailsJson = $"{{\"reason\":\"{eligibility.Reason}\",\"via\":\"{via}\"}}",
        });
        await db.SaveChangesAsync(ct);
        return "not_eligible";
    }

    /// <summary>Shared tail of JoinGroupAsync (open code) and AcceptGroupInvitationAsync (named
    /// invite): eligibility/capacity checks, GroupMember+Ticket creation, chat join.</summary>
    private async Task<ServiceResult<GroupMemberView>> AddMemberToGroupAsync(Group group, TicketType tt, Guid userId,
        string? displayName, IReadOnlyDictionary<string, string>? answers, CancellationToken ct)
    {
        // Group members are registrants too — same audience gate as order creation (V3 §4.4). Covers both
        // JoinGroupAsync and AcceptGroupInvitationAsync, which share this tail.
        if (await AudienceDenialReasonAsync(userId, group.EventId, "group", ct) is { } denied)
            return ServiceResult<GroupMemberView>.Fail(denied);

        var now = DateTime.UtcNow;
        if (now < tt.SaleStarts || now > tt.SaleEnds) return ServiceResult<GroupMemberView>.Fail("not_on_sale");

        if (await db.GroupMembers.AnyAsync(m => m.GroupId == group.Id && m.UserId == userId, ct))
            return ServiceResult<GroupMemberView>.Fail("already_joined");

        var fields = await db.FormFields.AsNoTracking()
            .Where(f => f.TicketTypeId == tt.Id && f.Scope == FormFieldScope.PerParticipant).ToListAsync(ct);
        var answersMap = answers ?? new Dictionary<string, string>();
        if (fields.Any(f => f.Required && !answersMap.ContainsKey(f.Key)))
            return ServiceResult<GroupMemberView>.Fail("missing_required_field");

        // Authoritative sold-out pre-check (§17.1): the pool, never the legacy Sold mirror. Skipped for
        // PerGroup, where this join takes no seat — otherwise the last team sold would be unable to fill
        // the roster it had already paid for.
        if (JoinConsumesInventory(tt) && await inventory.AvailableAsync(tt.Id, ct) < 1)
            return ServiceResult<GroupMemberView>.Fail("sold_out");

        var participantAnswersJson = ToJsonOrNull(fields, answersMap, FormFieldScope.PerParticipant);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var member = new GroupMember
        {
            GroupId = group.Id,
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(displayName) ? user.Name : displayName!,
            Phone = user.PhoneE164 ?? user.Phone,   // canonical column first (D-089)
            AnswersJson = participantAnswersJson,
            JoinedAt = now,
        };
        var orderItemId = await db.OrderItems.AsNoTracking()
            .Where(oi => oi.OrderId == group.OrderId).Select(oi => oi.Id).FirstAsync(ct);
        var ticket = NewTicket(orderItemId, group.EventId, userId, member.Id, participantAnswersJson);
        member.TicketId = ticket.Id;

        var poolId = await inventory.EnsureGeneralPoolAsync(tt.Id, tt.EventId, tt.Quantity, tt.Sold, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Both count-based limits are evaluated INSIDE the transaction, under advisory locks. Read
        // before it, each was a check-then-act two concurrent joins could both pass: the group would
        // overfill past OrderItem.Qty, and one buyer joining twice at once would exceed PerUserLimit.
        // Neither is expressible as a unique index — both are counts — so serialising the readers is
        // the guard, the same way SeatBlockService protects its reassign limit.
        //
        // Lock order is fixed (group, then buyer) and this is the only path taking both, so the pair
        // cannot deadlock against CreateOrderAsync, which takes the buyer lock alone.
        var (g1, g2) = (BitConverter.ToInt32(group.Id.ToByteArray(), 0), BitConverter.ToInt32(group.Id.ToByteArray(), 4));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({g1}, {g2})", ct);

        /*
         * D-357 — the team's size, which is no longer always `OrderItem.Qty`.
         *
         * `Qty` is now the BILLABLE quantity (1 for a PerGroup team), so reading it as the roster cap
         * would limit every paid team to one member. `Order.GroupSize` carries the real size; the
         * fallback to `Qty` is what keeps every pre-D-357 free-group order — where the two were the same
         * number — behaving exactly as before.
         */
        var orderRow = await db.Orders.AsNoTracking()
            .Where(o => o.Id == group.OrderId).Select(o => new { o.GroupSize }).FirstAsync(ct);
        var billableQty = await db.OrderItems.AsNoTracking()
            .Where(oi => oi.OrderId == group.OrderId).Select(oi => oi.Qty).FirstAsync(ct);
        var targetSize = orderRow.GroupSize ?? billableQty;
        var memberCount = await db.GroupMembers.CountAsync(m => m.GroupId == group.Id, ct);
        if (memberCount >= targetSize) return ServiceResult<GroupMemberView>.Fail("group_full");

        if (await BuyerLimitReachedAsync(tt, userId, null, ct))
            return ServiceResult<GroupMemberView>.Fail("limit_exceeded");

        /*
         * Conditional CONSUME (§17.1) — but only when a member IS a seat.
         *
         * Under `PerGroup` the whole team was bought and reserved as ONE unit, so a joining member takes
         * nothing further and `Quantity` finally means "number of teams": 50 teams stays 50 teams rather
         * than draining to 10 as rosters fill. Under `PerTicket` each person is a separately-priced seat
         * and still consumes one — which is every pre-D-357 row, so nothing existing moves.
         */
        if (JoinConsumesInventory(tt) && !await inventory.TryConsumeManyAsync([new PoolDraw(poolId, 1)], ct))
            return ServiceResult<GroupMemberView>.Fail("sold_out");
        db.GroupMembers.Add(member);
        db.Tickets.Add(ticket);
        if (JoinConsumesInventory(tt)) tt.Sold += 1;   // legacy mirror; the pool's Consumed is the authority

        await db.SaveChangesAsync(ct);
        await registration.ProjectOrderInTransactionAsync(group.OrderId, ct);   // member admission + credential, authoritative
        EnqueueChatJoin(group.EventId, userId);                                 // §17.1 side effect via outbox, never inline
        // D-359 — the Team roster follows the Group roster. Idempotent, so a member already on the team
        // (or a non-competition ticket type, which has no team at all) is a no-op.
        await teams.MaterialiseForGroupAsync(group.Id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<GroupMemberView>.Success(new(member.Id, member.UserId, member.Name, member.Phone, member.TicketId, member.AnswersJson, member.JoinedAt));
    }

    /// <summary>The next group number for an event, serialised against concurrent creators (D-299).
    ///
    /// <para><c>COUNT(*) + 1</c> alone is a lost-update race, and <c>groups (EventId, GroupNumber)</c> is
    /// UNIQUE — so two buyers creating a group on the same event in the same window both computed the same
    /// number and the second one's insert threw an unhandled unique violation. A 500 on a purchase, not a
    /// tidy "try again".</para>
    ///
    /// <para>A transaction-scoped advisory lock keyed on the event, the same idiom the per-buyer limit check
    /// already uses. Held only to the end of this transaction, and only contended by two people creating a
    /// group on the SAME event at the same instant — so it serialises nothing that matters. <c>MAX + 1</c>
    /// rather than <c>COUNT + 1</c>, because a deleted group would otherwise make the counter go
    /// backwards and collide with a number already issued.</para></summary>
    private async Task<int> NextGroupNumberAsync(Guid eventId, CancellationToken ct)
    {
        var key = BitConverter.ToInt32(eventId.ToByteArray(), 0);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({GroupNumberLockNamespace}, {key})", ct);
        var highest = await db.Groups.Where(g => g.EventId == eventId)
            .Select(g => (int?)g.GroupNumber).MaxAsync(ct);
        return (highest ?? 0) + 1;
    }

    /// <summary>Namespace for the group-number advisory lock, so it cannot collide with the per-buyer
    /// limit lock, which keys its first slot on a ticket-type id.</summary>
    private const int GroupNumberLockNamespace = 0x47_52_50_00;   // "GRP\0"

    /// <summary>Two 32-bit keys for a transaction-scoped advisory lock over (buyer, ticket type), same
    /// idiom as SeatBlockService.SeatLockKey. A key collision only makes two unrelated buyers serialise
    /// occasionally — harmless, never a correctness issue.</summary>
    private static (int, int) BuyerLimitLockKey(Guid ticketTypeId, Guid? userId, string? guestPhone)
        => (BitConverter.ToInt32(ticketTypeId.ToByteArray(), 0),
            userId is { } id
                ? BitConverter.ToInt32(id.ToByteArray(), 0)
                : BitConverter.ToInt32(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(guestPhone ?? "")), 0));

    /// <summary>Authoritative PerUserLimit check. MUST be called inside the order transaction: it takes a
    /// transaction-scoped advisory lock on (buyer, ticket type) and only then counts, so two simultaneous
    /// orders from one buyer serialise instead of both reading PerUserLimit-1 and both proceeding. The
    /// check before the transaction is only a friendly early-out — the limit counts tickets, so there is
    /// no unique index that could act as a backstop here.</summary>
    private async Task<bool> BuyerLimitReachedAsync(TicketType tt, Guid? userId, string? guestPhone, CancellationToken ct)
    {
        var (k1, k2) = BuyerLimitLockKey(tt.Id, userId, guestPhone);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({k1}, {k2})", ct);
        var count = userId is { } id
            ? await CountUserTicketsAsync(id, tt.Id, ct)
            : await CountGuestTicketsAsync(guestPhone!, tt.Id, ct);
        return count >= tt.PerUserLimit;
    }

    private async Task<int> CountUserTicketsAsync(Guid userId, Guid ticketTypeId, CancellationToken ct)
        => await (
            from t in db.Tickets
            join oi in db.OrderItems on t.OrderItemId equals oi.Id
            where t.UserId == userId && oi.TicketTypeId == ticketTypeId && t.State != TicketState.Void
            select t.Id).CountAsync(ct);

    private async Task<int> CountGuestTicketsAsync(string guestPhone, Guid ticketTypeId, CancellationToken ct)
        => await db.Orders.CountAsync(o => o.GuestPhone == guestPhone && o.TicketTypeId == ticketTypeId, ct);

    /// <param name="viewerUserId">When set, only tickets held by this user are returned — used by the
    /// per-user listing (D-062). Null keeps every ticket on the order, for the single-order views whose
    /// caller is already the buyer or a guest-token holder.</param>
    private async Task<OrderView> BuildOrderViewAsync(Order order, CancellationToken ct, Guid? viewerUserId = null)
    {
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.OrderId == order.Id, ct);
        var tickets = await (
            from t in db.Tickets.AsNoTracking()
            join oi in db.OrderItems.AsNoTracking() on t.OrderItemId equals oi.Id
            where oi.OrderId == order.Id && (viewerUserId == null || t.UserId == viewerUserId)
            select t).ToListAsync(ct);

        return ToOrderView(order, group, tickets);
    }

    /// <summary>The single OrderView shape, so the per-order path and the batched list path can never
    /// drift. Pure mapping — every caller supplies its own already-loaded group and tickets.</summary>
    private static OrderView ToOrderView(Order order, Group? group, IReadOnlyList<Ticket> tickets,
        string? eventTitle = null, string? eventSlug = null, string? ticketTypeName = null) =>
        new(order.Id, order.EventId, order.TicketTypeId, order.Status.ToString(), order.AmountPaise, order.Currency,
            order.RazorpayOrderId, group?.Id, group?.JoinCode, order.CreatedAt,
            tickets.Select(t => new TicketView(t.Id, t.Code, t.State.ToString(), t.CheckedInAt, t.AnswersJson, t.CreatedAt)).ToList(),
            GuestAccessToken: null, EventTitle: eventTitle, EventSlug: eventSlug, TicketTypeName: ticketTypeName);

    private async Task<GroupView?> BuildGroupViewAsync(Guid groupId, CancellationToken ct)
    {
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return null;

        var capacity = await db.OrderItems.AsNoTracking()
            .Where(oi => oi.OrderId == group.OrderId).Select(oi => oi.Qty).FirstOrDefaultAsync(ct);
        var members = await db.GroupMembers.AsNoTracking()
            .Where(m => m.GroupId == groupId).OrderBy(m => m.JoinedAt).ToListAsync(ct);

        // Group members' public identity is a visibility question, answered by the resolver rather than
        // by `ProfilePublic` (D-233) — one batched call for the group. Anonymous viewer: this view has
        // no caller identity threaded to it, so the answer matches the previous behaviour exactly.
        var memberUserIds = members.Where(m => m.UserId != null).Select(m => m.UserId!.Value).ToList();
        var linkable = await visibility.VisibleProfileIdsAsync(memberUserIds, null, ct);
        var users = await db.Users.AsNoTracking()
            .Where(u => linkable.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return ToGroupView(group, capacity, members, users);
    }

    private Ticket NewTicket(Guid orderItemId, Guid eventId, Guid? userId, Guid? groupMemberId, string? answersJson)
    {
        var code = Guid.NewGuid();
        return new Ticket
        {
            OrderItemId = orderItemId,
            EventId = eventId,
            UserId = userId,
            GroupMemberId = groupMemberId,
            Code = code,
            HmacSig = tokens.SignTicketCode(code),
            AnswersJson = answersJson,
        };
    }

    private async Task<string> GenerateUniqueJoinCodeAsync(CancellationToken ct)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity
        while (true)
        {
            var bytes = RandomNumberGenerator.GetBytes(6);
            var code = new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
            if (!await db.Groups.AnyAsync(g => g.JoinCode == code, ct)) return code;
        }
    }

    // 12-char alphanumeric, same generator shape as InvitationService.GenerateToken() — this is a
    // bearer credential embedded in a link (never typed manually), so it uses that token's higher-
    // entropy alphabet/length rather than the 6-char JoinCode alphabet meant for manual entry.
    private static string GenerateGuestAccessToken()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static string? ToJsonOrNull(List<FormField> fields, IReadOnlyDictionary<string, string> answers, FormFieldScope scope)
    {
        var scoped = fields.Where(f => f.Scope == scope && answers.ContainsKey(f.Key))
            .ToDictionary(f => f.Key, f => answers[f.Key]);
        return scoped.Count > 0 ? JsonSerializer.Serialize(scoped) : null;
    }

    // Re-splits the full answers stashed on a paid order (M10) into one scope's answers for ticket issuance.
    private static string? SplitScope(string? allAnswersJson, List<FormField> fields, FormFieldScope scope)
    {
        if (string.IsNullOrWhiteSpace(allAnswersJson)) return null;
        var all = JsonSerializer.Deserialize<Dictionary<string, string>>(allAnswersJson);
        return all is null ? null : ToJsonOrNull(fields, all, scope);
    }
}
