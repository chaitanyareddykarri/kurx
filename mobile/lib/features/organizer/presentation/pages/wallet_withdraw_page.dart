import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/utils/money.dart';
import '../providers/organizer_providers.dart';

/// Request a payout — `POST /v1/orgs/{orgId}/wallet/withdraw`.
///
/// ## What actually happens, stated plainly
///
/// The endpoint is real and the request is recorded: `WalletService.InitiateWithdrawalAsync`
/// validates the payout account and available balance, then inserts a `Withdrawal` row at status
/// `requested`.
///
/// **Nothing moves it after that.** `Withdrawal.Status` declares
/// `requested | processing | paid | rejected`, and no code anywhere assigns the other three — there
/// is no payout executor, and the gateway is `MockPaymentGateway`. So a request is a durable,
/// auditable intent, not a transfer.
///
/// This screen therefore submits for real and says exactly that. Showing "Payout on its way" would
/// be a lie about money, which is the one thing worth being pedantic about.
class WalletWithdrawPage extends ConsumerStatefulWidget {
  const WalletWithdrawPage({super.key, required this.orgId});
  final String orgId;

  @override
  ConsumerState<WalletWithdrawPage> createState() => _WalletWithdrawPageState();
}

class _WalletWithdrawPageState extends ConsumerState<WalletWithdrawPage> {
  final _amount = TextEditingController();
  bool _busy = false;
  String? _error;
  bool _submitted = false;

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
  }

  Future<void> _submit(int availablePaise) async {
    final rupees = double.tryParse(_amount.text.trim()) ?? 0;
    final paise = (rupees * 100).round();

    if (paise <= 0) {
      setState(() => _error = 'Enter an amount to withdraw.');
      return;
    }
    if (paise > availablePaise) {
      setState(() => _error = 'That is more than your available balance.');
      return;
    }

    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(orgSourceProvider).requestWithdrawal(widget.orgId, paise);
      ref.invalidate(orgWalletProvider(widget.orgId));
      if (!mounted) return;
      setState(() {
        _busy = false;
        _submitted = true;
      });
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = switch (e.code) {
          'payout_not_active' =>
            'Your payout account is not active yet. Complete organisation verification first.',
          'insufficient_balance' => 'Your available balance has changed. Refresh and try again.',
          'forbidden' => 'Only an Owner or Finance member can request a payout.',
          _ => e.userMessage,
        };
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Request payout')),
      body: AsyncValueView(
        value: ref.watch(orgWalletProvider(widget.orgId)),
        onRetry: () => ref.invalidate(orgWalletProvider(widget.orgId)),
        data: (wallet) => ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            Container(
              padding: const EdgeInsets.all(KSpace.lg),
              decoration: BoxDecoration(
                color: c.cardSurface,
                borderRadius: BorderRadius.circular(KRadius.lg),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Available to withdraw',
                      style: TextStyle(color: c.muted, fontSize: 13)),
                  const SizedBox(height: KSpace.xs),
                  Text(formatPaise(wallet.availablePaise, currency: wallet.currency),
                      style: TextStyle(
                          color: c.text, fontWeight: FontWeight.w800, fontSize: 28)),
                  const SizedBox(height: KSpace.md),
                  _Row(label: 'Collected', paise: wallet.collectedPaise, currency: wallet.currency),
                  _Row(label: 'Reserved', paise: wallet.reservedPaise, currency: wallet.currency),
                  _Row(label: 'Settled', paise: wallet.settledPaise, currency: wallet.currency),
                ],
              ),
            ),
            const SizedBox(height: KSpace.lg),
            _PayoutNotice(submitted: _submitted),
            const SizedBox(height: KSpace.lg),
            if (!_submitted) ...[
              TextField(
                controller: _amount,
                enabled: !_busy,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(labelText: 'Amount', prefixText: '₹ '),
              ),
              if (_error != null) ...[
                const SizedBox(height: KSpace.md),
                Semantics(
                  liveRegion: true,
                  child: Text(_error!,
                      style: TextStyle(color: Theme.of(context).colorScheme.error)),
                ),
              ],
              const SizedBox(height: KSpace.lg),
              FilledButton(
                onPressed: _busy ? null : () => _submit(wallet.availablePaise),
                child: _busy
                    ? const SizedBox(
                        height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('Submit payout request'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.paise, required this.currency});
  final String label;
  final int paise;
  final String currency;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(top: KSpace.xs),
      child: Row(
        children: [
          Expanded(child: Text(label, style: TextStyle(color: c.muted, fontSize: 13))),
          Text(formatPaise(paise, currency: currency), style: TextStyle(color: c.muted, fontSize: 13)),
        ],
      ),
    );
  }
}

class _PayoutNotice extends StatelessWidget {
  const _PayoutNotice({required this.submitted});
  final bool submitted;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // Material green beside a theme read, on a withdrawal's submitted state.
    final tone = submitted ? c.success : c.accent;

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
          Icon(submitted ? Icons.check_circle_outline_rounded : Icons.info_outline_rounded,
              color: tone, size: 22),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  submitted ? 'Payout request recorded' : 'Payouts are not executing yet',
                  style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15),
                ),
                const SizedBox(height: KSpace.xs),
                Text(
                  submitted
                      ? 'Your request is on file and will be processed once Kurx enables live '
                          'payouts. No money has moved yet.'
                      : 'Kurx runs against a mock payment gateway, so a request is queued and '
                          'audited but not paid out. Nothing will reach your bank until live '
                          'payouts are switched on.',
                  style: TextStyle(color: c.text.withValues(alpha: 0.85)),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
