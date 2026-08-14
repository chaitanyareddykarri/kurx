import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../providers/organizer_providers.dart';

class EventAnalyticsPage extends ConsumerWidget {
  const EventAnalyticsPage(
      {super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final param = (orgId: orgId, eventId: eventId);
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Event Analytics')),
      body: AsyncValueView(
        value: ref.watch(eventAnalyticsProvider(param)),
        onRetry: () => ref.invalidate(eventAnalyticsProvider(param)),
        data: (analytics) => ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            Row(
              children: [
                Expanded(
                  child: _KpiCard(
                    label: 'Revenue',
                    value: formatPaise(analytics.totalRevenuePaise),
                    icon: Icons.currency_rupee_rounded,
                    color: c.accent,
                  ),
                ),
                const SizedBox(width: KSpace.md),
                Expanded(
                  child: _KpiCard(
                    label: 'Tickets Sold',
                    value: '${analytics.ticketsSold}',
                    icon: Icons.confirmation_number_outlined,
                    color: c.teal,
                  ),
                ),
              ],
            ),
            const SizedBox(height: KSpace.md),
            Row(
              children: [
                Expanded(
                  child: _KpiCard(
                    label: 'Attendance Rate',
                    value:
                        '${(analytics.attendanceRate * 100).toStringAsFixed(1)}%',
                    icon: Icons.people_outline_rounded,
                    color: c.success,
                  ),
                ),
                const SizedBox(width: KSpace.md),
                Expanded(
                  child: _KpiCard(
                    label: 'Views',
                    value: '${analytics.totalViews}',
                    icon: Icons.visibility_outlined,
                    color: c.muted,
                  ),
                ),
              ],
            ),
            const SizedBox(height: KSpace.xl),
            DecoratedBox(
              decoration: BoxDecoration(
                color: c.cardSurface,
                borderRadius: BorderRadius.circular(KRadius.xl),
                boxShadow: kCardShadow(context),
              ),
              child: Padding(
                padding: const EdgeInsets.all(KSpace.xl),
                child: Column(
                  children: [
                    Icon(Icons.show_chart_rounded,
                        size: 48, color: c.muted),
                    const SizedBox(height: KSpace.md),
                    Text(
                      'Sales chart coming soon',
                      style: TextStyle(color: c.muted, fontSize: 13.5),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _KpiCard extends StatelessWidget {
  const _KpiCard({
    required this.label,
    required this.value,
    required this.icon,
    required this.color,
  });
  final String label;
  final String value;
  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
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
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(KRadius.md),
              ),
              child: Icon(icon, color: color, size: 18),
            ),
            const SizedBox(height: KSpace.md),
            Text(
              value,
              style: TextStyle(
                color: c.text,
                fontWeight: FontWeight.w900,
                fontSize: 20,
              ),
            ),
            Text(label,
                style: TextStyle(color: c.muted, fontSize: 12.5)),
          ],
        ),
      ),
    );
  }
}
