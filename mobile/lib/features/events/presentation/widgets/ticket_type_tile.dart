import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../../domain/entities/ticket_type.dart';

/// A ticket-type row: name, price (₹ from paise), availability, and an optional
/// select/book action (wired by the detail page's booking flow).
class TicketTypeTile extends StatelessWidget {
  const TicketTypeTile({super.key, required this.ticket, this.onBook, this.selected = false});

  final TicketType ticket;
  final VoidCallback? onBook;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final priceLabel = ticket.isFree ? 'Free' : Money.fromMinor(ticket.pricePaise);
    final tappable = onBook != null && !ticket.soldOut;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.lg, vertical: KSpace.xs + 2),
      child: Material(
        color: selected ? c.accent.withValues(alpha: 0.10) : c.elevated,
        borderRadius: BorderRadius.circular(KRadius.md),
        child: InkWell(
          onTap: tappable ? onBook : null,
          borderRadius: BorderRadius.circular(KRadius.md),
          child: Ink(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(KRadius.md),
              border: Border.all(color: selected ? c.accent : c.border),
            ),
            padding: const EdgeInsets.all(KSpace.lg),
            child: Row(
              children: [
                if (ticket.isAllAccess) ...[
                  Icon(Icons.workspace_premium_outlined, size: 18, color: c.accent),
                  const SizedBox(width: KSpace.sm),
                ],
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(ticket.name, style: TextStyle(color: c.text, fontSize: 15, fontWeight: FontWeight.w700)),
                      const SizedBox(height: 2),
                      Text(
                        ticket.soldOut
                            ? 'Sold out'
                            : ticket.available <= 20
                                ? 'Only ${ticket.available} left'
                                : '${ticket.available} available',
                        style: TextStyle(
                          color: ticket.soldOut
                              ? c.danger
                              : ticket.available <= 20
                                  ? c.accent
                                  : c.muted,
                          fontSize: 12.5,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: KSpace.md),
                Text(priceLabel, style: TextStyle(color: c.text, fontSize: 16, fontWeight: FontWeight.w800)),
                if (tappable) ...[
                  const SizedBox(width: KSpace.md),
                  Icon(Icons.chevron_right_rounded, color: c.muted),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
