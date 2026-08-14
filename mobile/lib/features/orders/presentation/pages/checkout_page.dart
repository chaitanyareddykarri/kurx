import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../../../events/presentation/providers/events_providers.dart';
import '../../data/models/attendee_dtos.dart';
import '../providers/attendee_providers.dart';

/// Checkout / order summary — `POST /v1/events/{eventId}/orders`.
///
/// ## Why this is a real screen and not a payment stop
///
/// The order endpoint works today. A **free** ticket type is settled inline: the backend issues the
/// ticket and returns the order already `Paid`, so the whole attendee journey completes here.
///
/// A **priced** ticket type is a different story. The backend creates a `Pending` order plus a
/// gateway order and hands back `razorpay_order_id` — but `PAYMENT_PROVIDER=mock`, so that id
/// belongs to `MockPaymentGateway` and no real money can move. Handing it to a real Razorpay SDK
/// would take a card and settle nothing.
///
/// So this screen completes free checkout for real, and for a priced ticket it **stops before
/// creating the order** and says why. It never fabricates a payment step.
class CheckoutPage extends ConsumerStatefulWidget {
  const CheckoutPage({
    super.key,
    required this.eventSlug,
    required this.eventId,
    required this.ticketTypeId,
  });

  final String eventSlug;
  final String eventId;
  final String ticketTypeId;

  @override
  ConsumerState<CheckoutPage> createState() => _CheckoutPageState();
}

class _CheckoutPageState extends ConsumerState<CheckoutPage> {
  /// Generated once per screen, not per tap: two taps are the same purchase intent, and the
  /// server's §17.1 atomic claim on this key is what stops a double-tap becoming two tickets.
  final String _idempotencyKey = DateTime.now().microsecondsSinceEpoch.toRadixString(36);

  bool _placing = false;
  String? _error;
  OrderDto? _order;

  Future<void> _placeOrder() async {
    setState(() {
      _placing = true;
      _error = null;
    });
    try {
      final order = await ref.read(attendeeActionsProvider).createOrder(
            widget.eventId,
            ticketTypeId: widget.ticketTypeId,
            idempotencyKey: _idempotencyKey,
          );
      if (!mounted) return;
      setState(() {
        _placing = false;
        _order = order;
      });
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _placing = false;
        _error = _explain(e);
      });
    }
  }

  /// Maps the order-path error codes to something an attendee can act on. Anything unrecognised
  /// falls back to [ApiError.userMessage] rather than being guessed at.
  String _explain(ApiError e) => switch (e.code) {
        'payments_not_enabled' =>
          'This organiser cannot take payments yet, so paid tickets are unavailable.',
        'paid_group_not_supported_yet' => 'Paid group bookings are not available yet.',
        'sold_out' || 'insufficient_inventory' =>
          'That ticket just sold out. Join the waitlist to be offered the next release.',
        'per_user_limit_reached' => 'You have already booked the maximum for this ticket.',
        'not_on_sale' => 'This ticket is not on sale right now.',
        'not_eligible' => 'This event is limited to a specific audience, and you are not on it.',
        _ => e.userMessage,
      };

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Checkout')),
      body: AsyncValueView(
        value: ref.watch(eventPageProvider(widget.eventSlug)),
        onRetry: () => ref.invalidate(eventPageProvider(widget.eventSlug)),
        data: (page) {
          final ticket = page.ticketTypes.where((t) => t.id == widget.ticketTypeId).firstOrNull;
          if (ticket == null) {
            return _Notice(
              icon: Icons.help_outline_rounded,
              tone: c.muted,
              title: 'Ticket unavailable',
              message: 'This ticket type is no longer on sale for ${page.detail.title}.',
            );
          }

          final order = _order;
          if (order != null) return _OrderPlaced(order: order, eventTitle: page.detail.title);

          final isPaid = ticket.pricePaise > 0;
          return ListView(
            padding: const EdgeInsets.all(KSpace.lg),
            children: [
              _SummaryCard(
                eventTitle: page.detail.title,
                ticketName: ticket.name,
                pricePaise: ticket.pricePaise,
                startsAt: page.detail.startsAt,
              ),
              const SizedBox(height: KSpace.lg),
              if (isPaid)
                _Notice(
                  icon: Icons.lock_outline_rounded,
                  tone: Theme.of(context).colorScheme.error,
                  title: 'Paid tickets are not live yet',
                  message:
                      'Kurx is running against a mock payment gateway, so this ticket cannot be '
                      'bought yet. Nothing has been charged and no order was created.\n\n'
                      'Free tickets check out normally.',
                )
              else ...[
                if (_error != null) ...[
                  Semantics(
                    liveRegion: true,
                    child: _Notice(
                      icon: Icons.error_outline_rounded,
                      tone: Theme.of(context).colorScheme.error,
                      title: 'Could not place your order',
                      message: _error!,
                    ),
                  ),
                  const SizedBox(height: KSpace.lg),
                ],
                KurxButton(
                  label: 'Confirm free ticket',
                  expand: true,
                  loading: _placing,
                  onPressed: _placing ? null : _placeOrder,
                ),
                const SizedBox(height: KSpace.md),
                Text(
                  'Your ticket is issued immediately and appears under My Tickets.',
                  style: TextStyle(color: c.muted, fontSize: 13),
                  textAlign: TextAlign.center,
                ),
              ],
            ],
          );
        },
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({
    required this.eventTitle,
    required this.ticketName,
    required this.pricePaise,
    required this.startsAt,
  });

  final String eventTitle;
  final String ticketName;
  final int pricePaise;
  final DateTime? startsAt;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // Central formatter (D-257). The ticket-type payload does not carry a currency yet, so this
    // renders at the INR default — but the symbol is no longer hardcoded at the call site.

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(eventTitle,
              style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 17)),
          if (startsAt != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(DateFormat('EEE d MMM yyyy, h:mm a').format(startsAt!.toLocal()),
                style: TextStyle(color: c.muted)),
          ],
          const Divider(height: KSpace.xl),
          Row(
            children: [
              Expanded(child: Text(ticketName, style: TextStyle(color: c.text))),
              Text(
                pricePaise == 0 ? 'Free' : formatPaise(pricePaise),
                style: TextStyle(color: c.text, fontWeight: FontWeight.w600),
              ),
            ],
          ),
          const SizedBox(height: KSpace.sm),
          Row(
            children: [
              Expanded(
                child: Text('Total',
                    style: TextStyle(color: c.text, fontWeight: FontWeight.w700)),
              ),
              Text(
                pricePaise == 0 ? formatPaise(0) : formatPaise(pricePaise),
                style: TextStyle(color: c.accent, fontWeight: FontWeight.w800, fontSize: 17),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _OrderPlaced extends StatelessWidget {
  const _OrderPlaced({required this.order, required this.eventTitle});

  final OrderDto order;
  final String eventTitle;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final ticket = order.tickets.firstOrNull;

    return ListView(
      padding: const EdgeInsets.all(KSpace.lg),
      children: [
        Icon(Icons.check_circle_rounded, size: 64, color: c.accent),
        const SizedBox(height: KSpace.lg),
        Text(
          order.isSettled ? 'You’re going!' : 'Order created',
          textAlign: TextAlign.center,
          style: TextStyle(color: c.text, fontWeight: FontWeight.w800, fontSize: 22),
        ),
        const SizedBox(height: KSpace.sm),
        Text(
          order.isSettled
              ? 'Your ticket for $eventTitle is confirmed.'
              : 'Your order for $eventTitle is pending payment.',
          textAlign: TextAlign.center,
          style: TextStyle(color: c.muted),
        ),
        const SizedBox(height: KSpace.xl),
        if (ticket != null)
          Container(
            padding: const EdgeInsets.all(KSpace.lg),
            decoration: BoxDecoration(
              color: c.cardSurface,
              borderRadius: BorderRadius.circular(KRadius.lg),
            ),
            child: Column(
              children: [
                Text('Ticket code', style: TextStyle(color: c.muted, fontSize: 13)),
                const SizedBox(height: KSpace.xs),
                SelectableText(
                  ticket.code,
                  style: TextStyle(
                    color: c.text,
                    fontWeight: FontWeight.w700,
                    fontFamily: 'monospace',
                  ),
                ),
              ],
            ),
          ),
        const SizedBox(height: KSpace.xl),
        KurxButton(
          label: 'View my tickets',
          expand: true,
          onPressed: () => context.go('/orders'),
        ),
      ],
    );
  }
}

class _Notice extends StatelessWidget {
  const _Notice({
    required this.icon,
    required this.tone,
    required this.title,
    required this.message,
  });

  final IconData icon;
  final Color tone;
  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: tone.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: Border.all(color: tone.withValues(alpha: 0.3)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: tone, size: 22),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title,
                    style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15)),
                const SizedBox(height: KSpace.xs),
                Text(message, style: TextStyle(color: c.text.withValues(alpha: 0.85))),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
