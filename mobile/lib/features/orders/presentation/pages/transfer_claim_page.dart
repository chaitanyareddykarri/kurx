import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/kurx_button.dart';
import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/orders_providers.dart';

/// Claim a transferred ticket via a transfer code.
class TransferClaimPage extends ConsumerStatefulWidget {
  const TransferClaimPage({super.key, this.code});

  /// Pre-filled when opened via deep-link.
  final String? code;

  @override
  ConsumerState<TransferClaimPage> createState() => _TransferClaimPageState();
}

class _TransferClaimPageState extends ConsumerState<TransferClaimPage> {
  late final TextEditingController _ctrl;
  bool _loading = false;

  @override
  void initState() {
    super.initState();
    _ctrl = TextEditingController(text: widget.code);
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Claim Your Ticket')),
      body: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Info banner
            DecoratedBox(
              decoration: BoxDecoration(
                color: c.accent.withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(KRadius.lg),
              ),
              child: Padding(
                padding: const EdgeInsets.all(KSpace.lg),
                child: Row(
                  children: [
                    Icon(Icons.swap_horiz_rounded, color: c.accent, size: 28),
                    const SizedBox(width: KSpace.md),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Someone sent you a ticket',
                            style: TextStyle(
                              color: c.text,
                              fontWeight: FontWeight.w700,
                              fontSize: 15,
                            ),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            'Enter the transfer code to accept it.',
                            style: TextStyle(color: c.muted, fontSize: 13),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: KSpace.xl),
            Text(
              'Transfer code',
              style: TextStyle(
                  color: c.text, fontWeight: FontWeight.w600, fontSize: 14),
            ),
            const SizedBox(height: KSpace.sm),
            TextField(
              controller: _ctrl,
              textCapitalization: TextCapitalization.characters,
              decoration: const InputDecoration(
                hintText: 'e.g. TRF-XXXXXXXX',
              ),
            ),
            const SizedBox(height: KSpace.xxl),
            // Warning
            Container(
              padding: const EdgeInsets.all(KSpace.md),
              decoration: BoxDecoration(
                color: c.warning.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(KRadius.md),
              ),
              child: Row(
                children: [
                  Icon(Icons.warning_amber_rounded,
                      color: c.warning, size: 18),
                  const SizedBox(width: KSpace.sm),
                  Expanded(
                    child: Text(
                      'Once claimed, this ticket will be permanently transferred to your account.',
                      style:
                          TextStyle(color: c.text, fontSize: 12),
                    ),
                  ),
                ],
              ),
            ),
            const Spacer(),
            KurxButton(
              label: _loading ? 'Claiming...' : 'Accept & Claim',
              expand: true,
              onPressed: _loading ? null : _claim,
            ),
            const SizedBox(height: KSpace.md),
            KurxButton(
              label: 'Decline',
              variant: KurxButtonVariant.secondary,
              expand: true,
              onPressed: () => Navigator.of(context).pop(),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _claim() async {
    final code = _ctrl.text.trim();
    if (code.isEmpty) return;
    setState(() => _loading = true);
    try {
      final transfer = await ref.read(ordersSourceProvider).claimTransfer(code);
      // The claim rotates the ticket's QR code server-side and reassigns it to us
      // (D-062). The rotated code is not in the claim response — it only appears in
      // a fresh list, which is also why the stale cached one must go.
      ref.invalidate(myOrdersProvider);
      final claimed = (await ref.read(myOrdersProvider.future))
          .expand((o) => o.tickets)
          .where((t) => t.id == transfer.ticketId);
      if (!mounted) return;
      KurxFeedback.success(context, "Ticket claimed. It's yours now.");
      // Replace, not push: the claim form is spent, and this page is often reached
      // by deep link with nothing worth returning to underneath it.
      if (claimed.isNotEmpty) {
        context.pushReplacement('/tickets/${claimed.first.code}');
      } else {
        context.pop();
      }
    } on ApiError catch (e) {
      if (!mounted) return;
      KurxFeedback.error(context, e.userMessage);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }
}
