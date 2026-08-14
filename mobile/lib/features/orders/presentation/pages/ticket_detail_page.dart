import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/orders_providers.dart';

/// Ticket Detail + QR — the real QR image from `GET /v1/tickets/{code}/qr.png`.
///
/// The QR arrives as bytes through Dio, not as an `Image.network` URL (D-302). That endpoint requires a
/// bearer token and `Image.network` sends none, so the previous version 401'd on every ticket and showed
/// its "QR unavailable" placeholder to everyone, always — a ticket screen that could not admit anyone.
class TicketDetailPage extends ConsumerWidget {
  const TicketDetailPage({super.key, required this.ticketCode});

  final String ticketCode;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final qr = ref.watch(ticketQrProvider(ticketCode));

    return Scaffold(
      backgroundColor: c.background,
      // The share action was removed rather than left inert (D-302): its handler was an empty block, so
      // it was a button that acknowledged nothing. A control that does nothing teaches a person the app
      // is broken, and there is no share implementation anywhere to wire it to.
      appBar: AppBar(title: const Text('Your Ticket')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // QR card — boarding-pass style
            DecoratedBox(
              decoration: BoxDecoration(
                color: c.cardSurface,
                borderRadius: BorderRadius.circular(KRadius.xl),
                boxShadow: kCardShadow(context),
                border: Border.all(
                  color: c.border,
                  style: BorderStyle.solid,
                ),
              ),
              child: Padding(
                padding: const EdgeInsets.all(KSpace.xl),
                child: Column(
                  children: [
                    // QR image from backend. White plate under it regardless of theme: a QR is read by
                    // a camera, and dark mode inverting the modules is how a scannable code stops
                    // scanning.
                    ClipRRect(
                      borderRadius: BorderRadius.circular(KRadius.md),
                      child: qr.when(
                        data: (bytes) => ColoredBox(
                          color: Colors.white,
                          child: Image.memory(
                            bytes,
                            width: 220,
                            height: 220,
                            fit: BoxFit.contain,
                            // Bytes decoded from a PNG the server just produced; a decode failure here
                            // is a corrupt response, not a missing ticket, so it reads the same as an
                            // error rather than as an empty box.
                            errorBuilder: (_, _, _) =>
                                _QrUnavailable(c: c, reason: 'QR could not be read'),
                          ),
                        ),
                        loading: () => SizedBox(
                          width: 220,
                          height: 220,
                          child: ColoredBox(
                            color: c.elevated,
                            child: const Center(child: CircularProgressIndicator()),
                          ),
                        ),
                        error: (_, _) => _QrUnavailable(c: c, reason: 'QR unavailable'),
                      ),
                    ),
                    const SizedBox(height: KSpace.lg),
                    Text(
                      ticketCode.toUpperCase().substring(0, 8),
                      style: TextStyle(
                        color: c.muted,
                        fontSize: 13,
                        letterSpacing: 2,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: KSpace.xl),
            // "Add to Wallet" was removed for the same reason as the share action (D-302): its handler
            // was empty, and no Apple/Google Wallet pass generation exists on the backend to call. It
            // returns when there is something behind it.
            _ActionButton(
              icon: Icons.swap_horiz_rounded,
              label: 'Transfer Ticket',
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute(
                  builder: (_) => _TransferSheet(ticketCode: ticketCode),
                ),
              ),
              secondary: false,
              c: c,
            ),
          ],
        ),
      ),
    );
  }
}

/// The QR's failure state. Shared by the fetch error and the decode error so a person sees one
/// consistent thing when there is nothing to scan.
class _QrUnavailable extends StatelessWidget {
  const _QrUnavailable({required this.c, required this.reason});
  final KurxColors c;
  final String reason;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 220,
      height: 220,
      color: c.elevated,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.qr_code_2_rounded, size: 64, color: c.muted),
          const SizedBox(height: KSpace.sm),
          Text(reason, style: TextStyle(color: c.muted, fontSize: 13)),
        ],
      ),
    );
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.icon,
    required this.label,
    required this.onTap,
    required this.secondary,
    required this.c,
  });
  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool secondary;
  final KurxColors c;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: secondary ? Colors.transparent : c.accent,
      borderRadius: BorderRadius.circular(KRadius.pill),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(KRadius.pill),
        child: Container(
          height: 56,
          decoration: secondary
              ? BoxDecoration(
                  border: Border.all(color: c.border),
                  borderRadius: BorderRadius.circular(KRadius.pill),
                )
              : null,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, color: secondary ? c.text : c.onAccent, size: 20),
              const SizedBox(width: KSpace.sm),
              Text(
                label,
                style: TextStyle(
                  color: secondary ? c.text : c.onAccent,
                  fontWeight: FontWeight.w700,
                  fontSize: 15,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Initiating a transfer (D-302).
///
/// **"Send Transfer" used to call nothing.** It ran `Navigator.pop(context)` — the sheet collected a
/// recipient's phone number, warned that transfers cannot be reclaimed, closed, and left no transfer
/// anywhere. `OrdersRemoteDataSource.initiateTransfer` existed the whole time and had no caller.
///
/// The API keys on the ticket's **id**, while this screen is routed by its **code**, so the id is
/// resolved from the orders already in the cache rather than by widening the route — no new fetch, and
/// no second source of truth for what a person's tickets are.
class _TransferSheet extends ConsumerStatefulWidget {
  const _TransferSheet({required this.ticketCode});
  final String ticketCode;

  @override
  ConsumerState<_TransferSheet> createState() => _TransferSheetState();
}

class _TransferSheetState extends ConsumerState<_TransferSheet> {
  // Owned by the State, not rebuilt in build(): a controller constructed inside build() is discarded
  // and recreated on every frame, losing what was typed and leaking the old one.
  final _ctrl = TextEditingController();
  bool _sending = false;
  String? _error;

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final phone = _ctrl.text.trim();
    if (phone.isEmpty) {
      setState(() => _error = 'Enter the recipient\'s phone number.');
      return;
    }

    setState(() {
      _sending = true;
      _error = null;
    });

    try {
      final orders = await ref.read(myOrdersProvider.future);
      final ticketId = orders
          .expand((o) => o.tickets)
          .firstWhere((t) => t.code == widget.ticketCode)
          .id;

      await ref.read(ordersSourceProvider).initiateTransfer(ticketId, phone);
      // The list a transferred ticket must disappear from.
      ref.invalidate(myOrdersProvider);
      if (!mounted) return;
      Navigator.pop(context);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Transfer sent.')),
      );
    } on StateError {
      // The code matched no ticket the caller owns — stale cache, or a ticket already transferred away.
      if (mounted) {
        setState(() => _error = 'That ticket is no longer in your account.');
      }
    } on ApiError catch (e) {
      if (mounted) setState(() => _error = e.detail ?? e.code);
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final ctrl = _ctrl;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Transfer Ticket')),
      body: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Recipient phone number',
                style: TextStyle(
                    color: c.text,
                    fontWeight: FontWeight.w600,
                    fontSize: 14)),
            const SizedBox(height: KSpace.sm),
            TextField(
              controller: ctrl,
              keyboardType: TextInputType.phone,
              decoration: const InputDecoration(
                hintText: 'Include the country code, e.g. +65 9123 4567',
              ),
            ),
            const SizedBox(height: KSpace.lg),
            Container(
              padding: const EdgeInsets.all(KSpace.md),
              decoration: BoxDecoration(
                color: c.warning.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(KRadius.md),
              ),
              child: Row(
                children: [
                  Icon(Icons.info_outline,
                      color: c.warning, size: 18),
                  const SizedBox(width: KSpace.sm),
                  Expanded(
                    child: Text(
                      'Transferred tickets cannot be reclaimed. The recipient must have a Kurx account.',
                      style: TextStyle(
                          color: c.text, fontSize: 12),
                    ),
                  ),
                ],
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: KSpace.md),
              Text(_error!, style: TextStyle(color: c.danger, fontSize: 13)),
            ],
            const Spacer(),
            Material(
              color: c.accent,
              borderRadius: BorderRadius.circular(KRadius.pill),
              child: InkWell(
                onTap: _sending ? null : _send,
                borderRadius: BorderRadius.circular(KRadius.pill),
                child: Container(
                  height: 56,
                  alignment: Alignment.center,
                  child: _sending
                      ? SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                              strokeWidth: 2, color: c.onAccent),
                        )
                      : Text('Send Transfer',
                          style: TextStyle(
                              color: c.onAccent,
                              fontWeight: FontWeight.w700,
                              fontSize: 15)),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
