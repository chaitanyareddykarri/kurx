import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/order_dto.dart';
import '../providers/orders_providers.dart';

class MyTicketsPage extends ConsumerStatefulWidget {
  const MyTicketsPage({super.key});

  @override
  ConsumerState<MyTicketsPage> createState() => _MyTicketsPageState();
}

class _MyTicketsPageState extends ConsumerState<MyTicketsPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 3, vsync: this);
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('My Tickets'),
        actions: [
          // The only way into the claim flow: a transfer arrives as an 8-char code
          // over WhatsApp, not a link, so the recipient needs somewhere to type it
          // (D-063).
          IconButton(
            icon: const Icon(Icons.swap_horiz_rounded),
            tooltip: 'Claim a transferred ticket',
            onPressed: () => context.push('/transfers/claim'),
          ),
        ],
        bottom: TabBar(
          controller: _tabs,
          indicatorColor: c.accent,
          labelColor: c.accent,
          unselectedLabelColor: c.muted,
          tabs: const [
            Tab(text: 'Active'),
            Tab(text: 'Used'),
            Tab(text: 'Expired'),
          ],
        ),
      ),
      body: AsyncValueView(
        value: ref.watch(myOrdersProvider),
        onRetry: () => ref.invalidate(myOrdersProvider),
        data: (orders) {
          // States are TicketState.ToLowerInvariant() over the wire — `issued`,
          // `checkedin`, `void` (OrderEndpoints.ToTicketJson). Matched exactly rather
          // than by a catch-all, so an unrecognised state shows up nowhere instead of
          // silently piling into one tab. A free order is Status=Paid/AmountPaise=0,
          // so `paid` covers it — there is no `free` status.
          final active = orders
              .where((o) => o.status == 'paid')
              .expand((o) => o.tickets)
              .where((t) => t.state == 'issued')
              .toList();
          final used = orders
              .expand((o) => o.tickets)
              .where((t) => t.state == 'checkedin')
              .toList();
          final expired = orders
              .expand((o) => o.tickets)
              .where((t) => t.state == 'void')
              .toList();

          return TabBarView(
            controller: _tabs,
            children: [
              _TicketList(tickets: active, orders: orders),
              _TicketList(tickets: used, orders: orders),
              _TicketList(tickets: expired, orders: orders),
            ],
          );
        },
      ),
    );
  }
}

class _TicketList extends StatelessWidget {
  const _TicketList({required this.tickets, required this.orders});

  final List<TicketDto> tickets;
  final List<OrderDto> orders;

  @override
  Widget build(BuildContext context) {
    if (tickets.isEmpty) {
      return const EmptyState(
        icon: Icons.confirmation_number_outlined,
        title: 'No tickets here',
        message: 'Tickets in this category will appear here.',
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(KSpace.lg),
      itemCount: tickets.length,
      separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
      itemBuilder: (context, i) {
        final t = tickets[i];
        return _TicketCard(ticket: t, onTap: () => context.push('/tickets/${t.code}'));
      },
    );
  }
}

class _TicketCard extends StatelessWidget {
  const _TicketCard({required this.ticket, required this.onTap});
  final TicketDto ticket;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final isUsed = ticket.state == 'checkedin';
    // A whole ticket card tappable through a bare GestureDetector announces as nothing at all —
    // no role, no name — so the way into a ticket was invisible to a screen reader (Phase 33's
    // defect class, on the card a buyer opens at the gate).
    return Semantics(
      button: true,
      label: isUsed ? 'Ticket, checked in' : 'Ticket, not yet checked in',
      child: GestureDetector(
      onTap: onTap,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: c.cardSurface,
          borderRadius: BorderRadius.circular(KRadius.xl),
          boxShadow: kCardShadow(context),
        ),
        child: Column(
          children: [
            // Top section
            Padding(
              padding: const EdgeInsets.all(KSpace.lg),
              child: Row(
                children: [
                  Container(
                    width: 52,
                    height: 52,
                    decoration: BoxDecoration(
                      color: isUsed ? c.elevated : c.accent.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(KRadius.md),
                    ),
                    child: Icon(
                      Icons.confirmation_number_rounded,
                      color: isUsed ? c.muted : c.accent,
                    ),
                  ),
                  const SizedBox(width: KSpace.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Ticket #${ticket.code.toString().substring(0, 8).toUpperCase()}',
                          style: TextStyle(
                            color: c.text,
                            fontWeight: FontWeight.w700,
                            fontSize: 15,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          DateFormat('dd MMM yyyy').format(ticket.createdAt),
                          style: TextStyle(color: c.muted, fontSize: 13),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(
                        horizontal: KSpace.md, vertical: KSpace.xs),
                    decoration: BoxDecoration(
                      color: isUsed
                          ? c.elevated
                          : c.accent.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(KRadius.pill),
                    ),
                    child: Text(
                      isUsed ? 'Used' : 'Active',
                      style: TextStyle(
                        color: isUsed ? c.muted : c.accent,
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            // Perforated divider
            _PerforatedDivider(color: c.border),
            // QR hint row
            Padding(
              padding: const EdgeInsets.symmetric(
                  horizontal: KSpace.lg, vertical: KSpace.md),
              child: Row(
                children: [
                  Icon(Icons.qr_code_2_rounded, color: c.muted, size: 20),
                  const SizedBox(width: KSpace.sm),
                  Text(
                    'Tap to view QR code',
                    style: TextStyle(color: c.muted, fontSize: 13),
                  ),
                  const Spacer(),
                  Icon(Icons.chevron_right_rounded, color: c.muted, size: 20),
                ],
              ),
            ),
          ],
        ),
      ),
      ),
    );
  }
}

class _PerforatedDivider extends StatelessWidget {
  const _PerforatedDivider({required this.color});
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _Notch(color: color, left: true),
        Expanded(
          child: LayoutBuilder(builder: (_, constraints) {
            final dashCount = (constraints.maxWidth / 10).floor();
            return Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: List.generate(
                dashCount,
                (_) => Container(width: 4, height: 1, color: color),
              ),
            );
          }),
        ),
        _Notch(color: color, left: false),
      ],
    );
  }
}

class _Notch extends StatelessWidget {
  const _Notch({required this.color, required this.left});
  final Color color;
  final bool left;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return SizedBox(
      width: 12,
      height: 24,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: c.background,
          border: Border.all(color: color),
          borderRadius: BorderRadius.horizontal(
            left: left ? Radius.zero : const Radius.circular(12),
            right: left ? const Radius.circular(12) : Radius.zero,
          ),
        ),
      ),
    );
  }
}
