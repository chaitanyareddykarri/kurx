import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../orders/data/models/attendee_dtos.dart';
import '../../../orders/presentation/providers/attendee_providers.dart';

/// Identity verification — `/v1/me/identity` (M3, D-042).
///
/// Only **masked last-4** values ever come back, so this screen can confirm what is on file but can
/// never re-display a number. Submissions are attempt-capped server-side (`too_many_attempts`,
/// 429) and run through `IKycProvider`, which is `MockKycProvider` in development — the submission
/// is real and recorded, the *adjudication* is mocked, and the screen says so rather than implying
/// a government check happened.
///
/// **Bank details are intentionally absent.** `POST /v1/me/identity/bank` exists, but bank/PAN
/// payout identity is part of the money path that is gated pending a security review; only the
/// government-ID and PAN identity steps are exposed here.
class IdentityVerificationPage extends ConsumerWidget {
  const IdentityVerificationPage({super.key});

  static const _idKinds = {
    'aadhaar': 'Aadhaar',
    'passport': 'Passport',
    'driving_licence': 'Driving licence',
    'voter_id': 'Voter ID',
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Identity verification')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(identityStatusProvider);
          await ref.read(identityStatusProvider.future);
        },
        child: AsyncValueView(
          value: ref.watch(identityStatusProvider),
          onRetry: () => ref.invalidate(identityStatusProvider),
          data: (status) => ListView(
            padding: const EdgeInsets.all(KSpace.lg),
            children: [
              _StatusBanner(status: status),
              const SizedBox(height: KSpace.xl),
              Text('Documents',
                  style:
                      TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15)),
              const SizedBox(height: KSpace.sm),
              _DocumentRow(
                icon: Icons.badge_outlined,
                label: status.govtIdKind != null
                    ? (_idKinds[status.govtIdKind] ?? 'Government ID')
                    : 'Government ID',
                last4: status.govtIdLast4,
                onSubmit: () => _submitGovtId(context, ref),
              ),
              const Divider(height: KSpace.xl),
              _DocumentRow(
                icon: Icons.receipt_long_outlined,
                label: 'PAN',
                last4: status.panLast4,
                onSubmit: () => _submitPan(context, ref),
              ),
              const SizedBox(height: KSpace.xl),
              Container(
                padding: const EdgeInsets.all(KSpace.lg),
                decoration: BoxDecoration(
                  color: c.cardSurface,
                  borderRadius: BorderRadius.circular(KRadius.lg),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(Icons.lock_outline_rounded, size: 20, color: c.muted),
                    const SizedBox(width: KSpace.md),
                    Expanded(
                      child: Text(
                        'Kurx stores only the last four digits of anything you submit. '
                        'Full numbers are never returned to this app.',
                        style: TextStyle(color: c.muted, fontSize: 13),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _submitGovtId(BuildContext context, WidgetRef ref) => _sheet(
        context,
        title: 'Verify government ID',
        builder: (setBusy, busy, error, close) => _GovtIdForm(
          kinds: _idKinds,
          busy: busy,
          error: error,
          onSubmit: (kind, number, name) async {
            setBusy(true, null);
            try {
              await ref
                  .read(attendeeActionsProvider)
                  .submitGovernmentId(kind: kind, idNumber: number, name: name);
              close();
            } on ApiError catch (e) {
              setBusy(false, _explain(e));
            }
          },
        ),
      );

  Future<void> _submitPan(BuildContext context, WidgetRef ref) => _sheet(
        context,
        title: 'Verify PAN',
        builder: (setBusy, busy, error, close) => _PanForm(
          busy: busy,
          error: error,
          onSubmit: (pan, name) async {
            setBusy(true, null);
            try {
              await ref.read(attendeeActionsProvider).submitPan(pan: pan, name: name);
              close();
            } on ApiError catch (e) {
              setBusy(false, _explain(e));
            }
          },
        ),
      );

  static String _explain(ApiError e) => switch (e.code) {
        'too_many_attempts' =>
          'Too many verification attempts. Please wait before trying again.',
        'invalid_pan' => 'That PAN does not look right. Check and try again.',
        'invalid_id_number' => 'That ID number does not look right. Check and try again.',
        _ => e.userMessage,
      };

  static Future<void> _sheet(
    BuildContext context, {
    required String title,
    required Widget Function(
      void Function(bool busy, String? error) setBusy,
      bool busy,
      String? error,
      VoidCallback close,
    ) builder,
  }) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: context.kurx.cardSurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(KRadius.xl)),
      ),
      builder: (ctx) => _SheetHost(title: title, builder: builder),
    );
  }
}

/// Owns the busy/error state for an identity sheet so each form stays a pure input widget.
class _SheetHost extends StatefulWidget {
  const _SheetHost({required this.title, required this.builder});

  final String title;
  final Widget Function(
    void Function(bool busy, String? error) setBusy,
    bool busy,
    String? error,
    VoidCallback close,
  ) builder;

  @override
  State<_SheetHost> createState() => _SheetHostState();
}

class _SheetHostState extends State<_SheetHost> {
  bool _busy = false;
  String? _error;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: EdgeInsets.fromLTRB(
        KSpace.lg,
        KSpace.lg,
        KSpace.lg,
        MediaQuery.viewInsetsOf(context).bottom + KSpace.lg,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(widget.title,
                style: TextStyle(color: c.text, fontWeight: FontWeight.w800, fontSize: 17)),
            const SizedBox(height: KSpace.lg),
            widget.builder(
              (busy, error) {
                if (!mounted) return;
                setState(() {
                  _busy = busy;
                  _error = error;
                });
              },
              _busy,
              _error,
              () {
                if (mounted) Navigator.of(context).pop();
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _GovtIdForm extends StatefulWidget {
  const _GovtIdForm({
    required this.kinds,
    required this.busy,
    required this.error,
    required this.onSubmit,
  });

  final Map<String, String> kinds;
  final bool busy;
  final String? error;
  final Future<void> Function(String kind, String number, String name) onSubmit;

  @override
  State<_GovtIdForm> createState() => _GovtIdFormState();
}

class _GovtIdFormState extends State<_GovtIdForm> {
  late String _kind = widget.kinds.keys.first;
  final _number = TextEditingController();
  final _name = TextEditingController();

  @override
  void dispose() {
    _number.dispose();
    _name.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        DropdownButtonFormField<String>(
          initialValue: _kind,
          decoration: const InputDecoration(labelText: 'Document type'),
          items: [
            for (final entry in widget.kinds.entries)
              DropdownMenuItem(value: entry.key, child: Text(entry.value)),
          ],
          onChanged: widget.busy ? null : (v) => setState(() => _kind = v ?? _kind),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: _number,
          enabled: !widget.busy,
          decoration: const InputDecoration(labelText: 'Document number'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: _name,
          enabled: !widget.busy,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Name exactly as printed'),
        ),
        if (widget.error != null) ...[
          const SizedBox(height: KSpace.md),
          Semantics(
            liveRegion: true,
            child: Text(widget.error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ),
        ],
        const SizedBox(height: KSpace.lg),
        FilledButton(
          onPressed: widget.busy
              ? null
              : () => widget.onSubmit(_kind, _number.text.trim(), _name.text.trim()),
          child: widget.busy
              ? const SizedBox(
                  height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : const Text('Submit for verification'),
        ),
      ],
    );
  }
}

class _PanForm extends StatefulWidget {
  const _PanForm({required this.busy, required this.error, required this.onSubmit});

  final bool busy;
  final String? error;
  final Future<void> Function(String pan, String name) onSubmit;

  @override
  State<_PanForm> createState() => _PanFormState();
}

class _PanFormState extends State<_PanForm> {
  final _pan = TextEditingController();
  final _name = TextEditingController();

  @override
  void dispose() {
    _pan.dispose();
    _name.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: _pan,
          enabled: !widget.busy,
          textCapitalization: TextCapitalization.characters,
          decoration: const InputDecoration(labelText: 'PAN', hintText: 'ABCDE1234F'),
        ),
        const SizedBox(height: KSpace.md),
        TextField(
          controller: _name,
          enabled: !widget.busy,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Name as on PAN'),
        ),
        if (widget.error != null) ...[
          const SizedBox(height: KSpace.md),
          Semantics(
            liveRegion: true,
            child: Text(widget.error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ),
        ],
        const SizedBox(height: KSpace.lg),
        FilledButton(
          onPressed: widget.busy
              ? null
              : () => widget.onSubmit(
                    _pan.text.trim().toUpperCase(),
                    _name.text.trim(),
                  ),
          child: widget.busy
              ? const SizedBox(
                  height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : const Text('Submit for verification'),
        ),
      ],
    );
  }
}

class _StatusBanner extends StatelessWidget {
  const _StatusBanner({required this.status});
  final IdentityStatusDto status;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final scheme = Theme.of(context).colorScheme;

    final (Color tone, IconData icon, String label, String detail) = switch (status.status) {
      // Material green on the identity-verified state, beside theme reads everywhere else: no dark
      // mode, never contrast-solved, on the badge that says an identity was confirmed.
      'verified' => (
          c.success,
          Icons.verified_user_rounded,
          'Verified',
          status.reviewedAt != null
              ? 'Confirmed ${DateFormat('d MMM yyyy').format(status.reviewedAt!.toLocal())}.'
              : 'Your identity is confirmed.'
        ),
      'pending' => (
          c.accent,
          Icons.hourglass_top_rounded,
          'Under review',
          'We are checking your documents. This usually takes a day.'
        ),
      'rejected' => (
          scheme.error,
          Icons.gpp_bad_outlined,
          'Not accepted',
          'Something did not match. You can submit again below.'
        ),
      _ => (
          c.muted,
          Icons.gpp_maybe_outlined,
          'Not verified',
          'Verify your identity to unlock organiser features and higher trust.'
        ),
    };

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
          Icon(icon, color: tone, size: 26),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Text(label,
                        style: TextStyle(
                            color: c.text, fontWeight: FontWeight.w700, fontSize: 16)),
                    if (status.level != null) ...[
                      const SizedBox(width: KSpace.sm),
                      Text(status.level!,
                          style: TextStyle(color: tone, fontWeight: FontWeight.w600)),
                    ],
                  ],
                ),
                const SizedBox(height: KSpace.xs),
                Text(detail, style: TextStyle(color: c.text.withValues(alpha: 0.85))),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _DocumentRow extends StatelessWidget {
  const _DocumentRow({
    required this.icon,
    required this.label,
    required this.last4,
    required this.onSubmit,
  });

  final IconData icon;
  final String label;
  final String? last4;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final onFile = last4 != null && last4!.isNotEmpty;

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Icon(icon, color: onFile ? c.accent : c.muted),
      title: Text(label, style: TextStyle(color: c.text)),
      subtitle: Text(
        onFile ? 'On file · ends $last4' : 'Not submitted',
        style: TextStyle(color: c.muted),
      ),
      trailing: TextButton(
        onPressed: onSubmit,
        child: Text(onFile ? 'Replace' : 'Verify'),
      ),
    );
  }
}
