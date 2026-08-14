import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../../data/models/order_dto.dart';
import '../providers/orders_providers.dart';

class MyOrdersPage extends ConsumerStatefulWidget {
  const MyOrdersPage({super.key});

  @override
  ConsumerState<MyOrdersPage> createState() => _MyOrdersPageState();
}

class _MyOrdersPageState extends ConsumerState<MyOrdersPage>
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
        title: const Text('My Orders'),
        actions: [
          IconButton(
            icon: const Icon(Icons.receipt_long_outlined),
            tooltip: 'Refunds',
            onPressed: () => context.push('/refunds'),
          ),
        ],
        bottom: TabBar(
          controller: _tabs,
          indicatorColor: c.accent,
          labelColor: c.accent,
          unselectedLabelColor: c.muted,
          tabs: const [
            Tab(text: 'Upcoming'),
            Tab(text: 'Past'),
            Tab(text: 'Cancelled'),
          ],
        ),
      ),
      body: AsyncValueView(
        value: ref.watch(myOrdersProvider),
        onRetry: () => ref.invalidate(myOrdersProvider),
        data: (orders) {
          final upcoming = orders
              .where((o) => o.status == 'paid' || o.status == 'free')
              .toList();
          final past = orders
              .where((o) => o.status == 'attended' || o.status == 'completed')
              .toList();
          final cancelled = orders
              .where((o) =>
                  o.status == 'cancelled' || o.status == 'refunded')
              .toList();

          return TabBarView(
            controller: _tabs,
            children: [
              _OrderList(orders: upcoming),
              _OrderList(orders: past),
              _OrderList(orders: cancelled),
            ],
          );
        },
      ),
    );
  }
}

class _OrderList extends StatelessWidget {
  const _OrderList({required this.orders});
  final List<OrderDto> orders;

  @override
  Widget build(BuildContext context) {
    if (orders.isEmpty) {
      return const EmptyState(
        icon: Icons.receipt_long_outlined,
        title: 'No orders here',
        message: 'Your orders will appear here.',
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(KSpace.lg),
      itemCount: orders.length,
      separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
      itemBuilder: (_, i) => _OrderCard(order: orders[i]),
    );
  }
}

class _OrderCard extends StatelessWidget {
  const _OrderCard({required this.order});
  final OrderDto order;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final status = order.status;
    final (statusLabel, statusColor) = switch (status) {
      'paid' => ('Paid', c.success),
      'free' => ('Free', c.success),
      'pending' => ('Pending', c.warning),
      'cancelled' => ('Cancelled', c.danger),
      'refunded' => ('Refunded', c.muted),
      _ => (status, c.muted),
    };

    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.xl),
        boxShadow: kCardShadow(context),
      ),
      child: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Order #${order.id.substring(0, 8).toUpperCase()}',
                    style: TextStyle(
                      color: c.text,
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                    ),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(
                      horizontal: KSpace.md, vertical: KSpace.xs),
                  decoration: BoxDecoration(
                    color: statusColor.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(KRadius.pill),
                  ),
                  child: Text(
                    statusLabel,
                    style: TextStyle(
                      color: statusColor,
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: KSpace.sm),
            Text(
              DateFormat('dd MMM yyyy, hh:mm a').format(order.createdAt),
              style: TextStyle(color: c.muted, fontSize: 12.5),
            ),
            const SizedBox(height: KSpace.md),
            Row(
              children: [
                Icon(Icons.confirmation_number_outlined,
                    color: c.muted, size: 16),
                const SizedBox(width: KSpace.xs),
                Text(
                  '${order.tickets.length} ticket${order.tickets.length == 1 ? '' : 's'}',
                  style: TextStyle(color: c.muted, fontSize: 13),
                ),
                const Spacer(),
                Text(
                  order.amountPaise == 0
                      ? 'Free'
                      : formatPaise(order.amountPaise, currency: order.currency),
                  style: TextStyle(
                    color: c.text,
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
