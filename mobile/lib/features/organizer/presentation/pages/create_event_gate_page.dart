import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../auth/domain/entities/current_user.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../../core/theme/design_tokens.dart';

/// The Create-Event gate (D-305) — the Flutter mirror of `web/components/host/create-event-gate.tsx`.
///
/// Same two screens, same rules, same words. Web and the app are one product: a person who learns this
/// flow on one must recognise it on the other, so the copy is deliberately identical rather than
/// "equivalent". Only the presentation is platform-shaped (cards and a bottom button here, a stepper
/// there).
///
///   ① Eligibility — reads the caller's EXISTING verification state; never re-asks a passed check.
///   ② Public or Private — a capability decision, not a form field.
///   ③ (deliberately absent) — D-305 resolved that Public and Private carry the SAME verification, so
///      there is no second pass. Documented as skipped, not silently dropped.
///
/// **Never blocks a free event.** `canOrganizeFree` is true for any account, so ① is informational for
/// free hosting and only paid hosting has a bar.
class CreateEventGatePage extends ConsumerStatefulWidget {
  const CreateEventGatePage({super.key});

  @override
  ConsumerState<CreateEventGatePage> createState() => _CreateEventGatePageState();
}

class _CreateEventGatePageState extends ConsumerState<CreateEventGatePage> {
  int _stage = 0; // 0 = eligibility, 1 = product
  String? _product;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // Read from the live server-derived capabilities, never re-derived here — the same values gate the
    // money path in OrderService, so a local guess that disagreed would promise a sale the server
    // refuses. `trust` defaults to the closed position when there is no user.
    final trust = ref.watch(currentUserProvider)?.trust;
    final canHostPaid = trust?.canOrganizePaid ?? false;
    // D-307 — the server's answer, never re-derived here. Closed position on absence.
    final canPublic = trust?.canCreatePublicEvent ?? false;
    final canPrivate = trust?.canCreatePrivateEvent ?? true;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Create Event')),
      body: SafeArea(
        child: _stage == 0
            ? _eligibility(context, canHostPaid)
            : _productChoice(context, canPublic, canPrivate, trust),
      ),
    );
  }

  Widget _eligibility(BuildContext context, bool canHostPaid) {
    final c = context.kurx;
    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(KSpace.lg),
            children: [
              Text('Before you start',
                  style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
              const SizedBox(height: KSpace.xs),
              Text(
                "Here's what your account can do today. Anything already verified is done — you won't be asked again.",
                style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35),
              ),
              const SizedBox(height: KSpace.lg),
              const _CheckRow(
                done: true,
                label: 'Create a free event',
                detail: 'Any signed-in account. Nothing to verify.',
              ),
              const SizedBox(height: KSpace.sm),
              _CheckRow(
                done: canHostPaid,
                label: 'Sell tickets',
                detail: canHostPaid
                    ? 'Identity, PAN and bank account are verified.'
                    : 'Needs identity, PAN and a verified bank account. You can create the event now and finish this before you publish.',
              ),
              if (!canHostPaid) ...[
                const SizedBox(height: KSpace.lg),
                Container(
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    border: Border.all(color: c.border),
                    borderRadius: BorderRadius.circular(KRadius.md),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Verification lives in your profile',
                          style: TextStyle(color: c.text, fontWeight: FontWeight.w600, fontSize: 14)),
                      const SizedBox(height: KSpace.xs),
                      // Deliberately not a blocker: a free event needs none of this, and stopping here
                      // would invent a requirement the platform does not have.
                      Text("You don't need any of this to create a free event.",
                          style: TextStyle(color: c.muted, fontSize: 12.5)),
                    ],
                  ),
                ),
              ],
            ],
          ),
        ),
        _BottomAction(label: 'Continue', onPressed: () => setState(() => _stage = 1)),
      ],
    );
  }

  Widget _productChoice(BuildContext context, bool canPublic, bool canPrivate,
      TrustCapabilities? trust) {
    final c = context.kurx;
    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(KSpace.lg),
            children: [
              Text('What kind of event is this?',
                  style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
              const SizedBox(height: KSpace.xs),
              Text(
                "This decides which event types you can choose from. It isn't about who can see the event — you set that separately.",
                style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35),
              ),
              const SizedBox(height: KSpace.lg),
              _ProductCard(
                selected: _product == 'Public',
                icon: Icons.public_rounded,
                title: 'Public',
                description:
                    "Open to people who don't already know you — meetups, fests, workshops, conferences, fundraisers.",
                onTap: () => setState(() => _product = 'Public'),
              ),
              const SizedBox(height: KSpace.sm),
              _ProductCard(
                selected: _product == 'Private',
                icon: Icons.lock_outline_rounded,
                title: 'Private',
                description:
                    'For a group you already have — weddings, parties, reunions, internal company events.',
                onTap: () => setState(() => _product = 'Private'),
              ),
              const SizedBox(height: KSpace.lg),
              // D-307. A free public event still carries the platform's name and reaches every user
              // through discovery, so Public asks for the full set whether or not money moves. Only the
              // MISSING items are listed — a passed check is never asked for again.
              if (_product == 'Public' && !canPublic)
                Container(
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    border: Border.all(color: c.border),
                    borderRadius: BorderRadius.circular(KRadius.md),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Verify your identity to host public events',
                          style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 14)),
                      const SizedBox(height: KSpace.xs),
                      Text(
                        'Public events are open to everyone on Kurx, so we verify who is behind them — whether the event is free or paid.',
                        style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
                      ),
                      const SizedBox(height: KSpace.sm),
                      _Sub(done: trust?.identityVerified ?? false, label: 'Government ID or PAN approved'),
                      _Sub(done: trust?.bankVerified ?? false, label: 'Bank account approved'),
                      // Penny drop and name match are the BANK OWNERSHIP links; they live inside
                      // bankVerified, so they are named here rather than tested separately.
                      _Sub(
                          done: trust?.bankVerified ?? false,
                          label: 'Bank ownership confirmed (penny drop) and name matched'),
                      const SizedBox(height: KSpace.sm),
                      Text('Complete verification in Profile → Verification.',
                          style: TextStyle(color: c.muted, fontSize: 12.5)),
                    ],
                  ),
                ),
              // Private never asks for the financial chain: it cannot be listed, cannot take payment,
              // and reaches no discovery surface.
              if (_product == 'Private')
                Container(
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    color: c.elevated,
                    borderRadius: BorderRadius.circular(KRadius.md),
                  ),
                  child: Text(
                    "Private events need no financial verification. A private event is invitation-only, never appears in search or on Home, and cannot sell tickets — so we don't ask for bank details.",
                    style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
                  ),
                ),
            ],
          ),
        ),
        _BottomAction(
          label: 'Continue',
          onPressed: _product == null ||
                  (_product == 'Public' ? !canPublic : !canPrivate)
              ? null
              : () => context.push('/events/create/form?product=$_product'),
          onBack: () => setState(() => _stage = 0),
        ),
      ],
    );
  }
}

/// One outstanding requirement. Mirrors web's `Sub`.
class _Sub extends StatelessWidget {
  const _Sub({required this.done, required this.label});
  final bool done;
  final String label;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Row(
        children: [
          Icon(done ? Icons.check_circle_rounded : Icons.radio_button_unchecked_rounded,
              size: 14, color: done ? c.accent : c.muted),
          const SizedBox(width: KSpace.sm),
          Expanded(
            child: Text(label,
                style: TextStyle(color: done ? c.text : c.muted, fontSize: 12.5)),
          ),
        ],
      ),
    );
  }
}

class _CheckRow extends StatelessWidget {
  const _CheckRow({required this.done, required this.label, required this.detail});
  final bool done;
  final String label;
  final String detail;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        border: Border.all(color: c.border),
        borderRadius: BorderRadius.circular(KRadius.md),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(done ? Icons.check_circle_rounded : Icons.radio_button_unchecked_rounded,
              size: 18, color: done ? c.accent : c.muted),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: TextStyle(color: c.text, fontSize: 14)),
                const SizedBox(height: 2),
                Text(detail, style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.3)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ProductCard extends StatelessWidget {
  const _ProductCard({
    required this.selected,
    required this.icon,
    required this.title,
    required this.description,
    required this.onTap,
  });
  final bool selected;
  final IconData icon;
  final String title;
  final String description;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Semantics(
      selected: selected,
      button: true,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(KRadius.md),
        child: Container(
          padding: const EdgeInsets.all(KSpace.md),
          decoration: BoxDecoration(
            border: Border.all(color: selected ? c.accent : c.border, width: selected ? 2 : 1),
            borderRadius: BorderRadius.circular(KRadius.md),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(icon, size: 20, color: c.text),
                  const SizedBox(width: KSpace.sm),
                  Text(title,
                      style: TextStyle(color: c.text, fontSize: 15, fontWeight: FontWeight.w700)),
                ],
              ),
              const SizedBox(height: KSpace.xs),
              Text(description, style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35)),
            ],
          ),
        ),
      ),
    );
  }
}

class _BottomAction extends StatelessWidget {
  const _BottomAction({required this.label, required this.onPressed, this.onBack});
  final String label;
  final VoidCallback? onPressed;
  final VoidCallback? onBack;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.all(KSpace.lg),
      child: Row(
        children: [
          if (onBack != null)
            TextButton(onPressed: onBack, child: const Text('Back')),
          const Spacer(),
          Material(
            color: onPressed == null ? c.elevated : c.accent,
            borderRadius: BorderRadius.circular(KRadius.pill),
            child: InkWell(
              onTap: onPressed,
              borderRadius: BorderRadius.circular(KRadius.pill),
              child: Container(
                height: 48,
                padding: const EdgeInsets.symmetric(horizontal: KSpace.xl),
                alignment: Alignment.center,
                child: Text(label,
                    style: TextStyle(
                        color: onPressed == null ? c.muted : c.onAccent,
                        fontWeight: FontWeight.w700,
                        fontSize: 15)),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
