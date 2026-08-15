import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../auth/domain/entities/current_user.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/organizer_providers.dart';

/// The Create-Event gate (D-305, tiered by D-343) — the Flutter mirror of
/// `web/components/host/create-event-gate.tsx`.
///
/// Same two screens, same rules, same words. Web and the app are one product: a person who learns this
/// flow on one must recognise it on the other, so the copy is deliberately identical rather than
/// "equivalent". Only the presentation is platform-shaped (cards and a bottom button here, a stepper
/// there).
///
///   ① Who is it for?   Public or Private — a capability decision, not a form field.
///   ② Are you charging? Free or Paid, with the requirements THAT pair actually needs.
///
/// **The two questions select a verification tier between them (D-343):**
///
/// | Product | Money | Requires                                               |
/// |---------|-------|--------------------------------------------------------|
/// | Private | Free  | nothing                                                |
/// | Public  | Free  | identity — government ID *or* PAN                      |
/// | Public  | Paid  | identity + PAN + bank + penny drop + holder-name match  |
/// | Private | Paid  | impossible — a Private event can never sell            |
///
/// The screen this replaced ("Before you start") listed those capabilities as bordered cards with
/// circled check icons — the same chrome the real selectors use one screen later — so people tapped
/// them expecting to choose free or paid and nothing happened. The information was right and the
/// affordance was a lie.
class CreateEventGatePage extends ConsumerStatefulWidget {
  const CreateEventGatePage({super.key});

  @override
  ConsumerState<CreateEventGatePage> createState() => _CreateEventGatePageState();
}

class _CreateEventGatePageState extends ConsumerState<CreateEventGatePage> {
  int _stage = 0; // 0 = product, 1 = pricing
  String? _product;
  String _pricing = 'free';

  /// A Private event can never sell, so choosing it resets the money answer rather than carrying a
  /// stale 'paid' into a product that cannot honour it.
  void _chooseProduct(String next) => setState(() {
        _product = next;
        if (next == 'Private') _pricing = 'free';
      });

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // Read from the live server-derived capabilities, never re-derived here — the same values gate the
    // money path in OrderService, so a local guess that disagreed would promise a sale the server
    // refuses. `trust` defaults to the closed position when there is no user.
    final trust = ref.watch(currentUserProvider)?.trust;
    final canHostPaid = trust?.canOrganizePaid ?? false;
    // D-307/D-343 — the server's answer, now the IDENTITY tier alone. Closed position on absence.
    final canPublic = trust?.canCreatePublicEvent ?? false;
    final canPrivate = trust?.canCreatePrivateEvent ?? true;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Create Event')),
      body: SafeArea(
        child: _stage == 0
            ? _productChoice(context, canPublic, canPrivate)
            : _pricingChoice(context, canPublic, canHostPaid, trust),
      ),
    );
  }

  Widget _productChoice(BuildContext context, bool canPublic, bool canPrivate) {
    final c = context.kurx;
    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(KSpace.lg),
            children: [
              Text('Who is this event for?',
                  style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
              const SizedBox(height: KSpace.xs),
              Text(
                "This decides which event types you can choose from. It isn't about who can see the event — you set that separately.",
                style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35),
              ),
              const SizedBox(height: KSpace.lg),
              _ChoiceCard(
                selected: _product == 'Public',
                icon: Icons.public_rounded,
                title: 'Public',
                description:
                    "Open to people who don't already know you — meetups, fests, workshops, conferences, fundraisers.",
                note: canPublic ? null : 'needs verification',
                onTap: () => _chooseProduct('Public'),
              ),
              const SizedBox(height: KSpace.sm),
              _ChoiceCard(
                selected: _product == 'Private',
                icon: Icons.lock_outline_rounded,
                title: 'Private',
                description:
                    'For a group you already have — weddings, parties, reunions, internal company events.',
                note: canPrivate ? null : 'needs verification',
                onTap: () => _chooseProduct('Private'),
              ),
            ],
          ),
        ),
        _BottomAction(
          label: 'Continue',
          onPressed: _product == null ? null : () => setState(() => _stage = 1),
        ),
      ],
    );
  }

  Widget _pricingChoice(
      BuildContext context, bool canPublic, bool canHostPaid, TrustCapabilities? trust) {
    final c = context.kurx;
    final isPublic = _product == 'Public';
    // D-350 — selling also needs a VERIFIED organisation to represent, because settlement is keyed on
    // one. The server enforces this at submit-for-review and again at capture; asking here is what stops
    // someone filling in eleven steps for an event that can never sell. Closed position on a list that
    // has not loaded, matching the wizard's `_representingValid`.
    // Under the bypass the server accepts a paid event with no organisation at all, so demanding one
    // here would refuse what the server would take.
    final requiresRepresentation = trust?.requiresRepresentation ?? true;
    final canRepresentVerified = !requiresRepresentation ||
        ref.watch(myRepresentationsProvider).maybeWhen(
          data: (list) => list.any((r) => r.canBackPaidEvent || r.isVerified),
          orElse: () => false,
        );
    // Public is refused by the identity tier; Paid by the financial one, and now also by having no
    // verified organisation. Separate reasons, because they are answered in three different places and
    // a merged message would send someone to the wrong one.
    final publicBlocked = isPublic && !canPublic;
    final paidBlocked = _pricing == 'paid' && (!canHostPaid || !canRepresentVerified);

    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(KSpace.lg),
            children: [
              Text('Are you charging for tickets?',
                  style: TextStyle(color: c.text, fontSize: 20, fontWeight: FontWeight.w800)),
              const SizedBox(height: KSpace.xs),
              Text(
                isPublic
                    ? 'A free public event only needs to prove who you are. Selling tickets also needs the bank account the money settles into.'
                    : "Private events are always free — they can't sell tickets.",
                style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35),
              ),
              const SizedBox(height: KSpace.lg),
              _ChoiceCard(
                selected: _pricing == 'free',
                icon: Icons.card_giftcard_rounded,
                title: 'Free',
                description: 'No ticket charges. Anyone can register.',
                onTap: () => setState(() => _pricing = 'free'),
              ),
              const SizedBox(height: KSpace.sm),
              _ChoiceCard(
                selected: _pricing == 'paid',
                icon: Icons.confirmation_number_outlined,
                title: 'Paid',
                description: isPublic
                    ? 'Sell tickets. You set the price in the form.'
                    : 'Not available for a private event.',
                note: !isPublic
                    ? null
                    : !canHostPaid
                        ? 'needs bank verification'
                        : !canRepresentVerified
                            ? 'needs a verified organisation'
                            : null,
                disabled: !isPublic,
                onTap: () {
                  if (isPublic) setState(() => _pricing = 'paid');
                },
              ),
              const SizedBox(height: KSpace.lg),
              // Private asks for nothing at all — it cannot be listed, cannot take payment, and reaches
              // no discovery surface, so there is no exposure to bound and no money to settle.
              if (!isPublic)
                Container(
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    color: c.elevated,
                    borderRadius: BorderRadius.circular(KRadius.md),
                  ),
                  child: Text(
                    "Nothing to verify. A private event is invitation-only, never appears in search or on Home, and cannot sell tickets — so we don't ask for identity or bank details.",
                    style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
                  ),
                )
              else
                Container(
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    border: Border.all(color: c.border),
                    borderRadius: BorderRadius.circular(KRadius.md),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                          _pricing == 'paid'
                              ? 'What selling tickets needs'
                              : 'What a free public event needs',
                          style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 14)),
                      const SizedBox(height: KSpace.xs),
                      Text(
                        _pricing == 'paid'
                            ? 'Money settles into a bank account, so we confirm the account is real and yours.'
                            : "Public events reach everyone on Kurx through search and Home, so we verify who is behind them. We don't ask for bank details — nothing is being paid.",
                        style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
                      ),
                      const SizedBox(height: KSpace.sm),
                      // The identity tier — the whole bar for a free public event. PAN is deliberately
                      // not named here: it is a tax identity and a free event reports no income (D-343).
                      _Sub(
                          done: trust?.identityVerified ?? false,
                          label: 'Government ID or PAN approved'),
                      // The financial tier, shown only when money is actually involved. Penny drop and
                      // name match are the bank-OWNERSHIP links inside bankVerified, named so the person
                      // knows what is outstanding — they are not extra predicates.
                      if (_pricing == 'paid') ...[
                        _Sub(done: trust?.bankVerified ?? false, label: 'PAN approved'),
                        _Sub(done: trust?.bankVerified ?? false, label: 'Bank account approved'),
                        _Sub(
                            done: trust?.bankVerified ?? false,
                            label: 'Bank ownership confirmed (penny drop) and name matched'),
                        // D-350 — not a proof about the person: ticket money settles to an organisation,
                        // so a paid event must represent a verified one.
                        _Sub(
                            done: canRepresentVerified,
                            label: 'A verified organisation to represent'),
                      ],
                      // Spelled out rather than left as a ticked-off row: this is the one requirement
                      // that is not about the person, and "you cannot sell as yourself" is surprising
                      // enough to deserve the sentence.
                      if (_pricing == 'paid' && canHostPaid && !canRepresentVerified) ...[
                        const SizedBox(height: KSpace.sm),
                        Text(
                          "Ticket money settles into an organisation's account, so a paid event has to "
                          "represent one — you can't sell as yourself. Free events have no such requirement.",
                          style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
                        ),
                      ],
                      if (publicBlocked || paidBlocked) ...[
                        const SizedBox(height: KSpace.sm),
                        Text('Complete verification in Profile → Verification.',
                            style: TextStyle(color: c.muted, fontSize: 12.5)),
                      ],
                    ],
                  ),
                ),
              // D-323 — the capability is open while the proofs are not on file, which happens on
              // exactly one condition: IDENTITY_VERIFICATION_BYPASS is set, and it cannot be set in
              // Production. Saying "verified" would forge the fact the bypass is forbidden from forging.
              if (isPublic && canPublic && !(trust?.identityVerified ?? false)) ...[
                const SizedBox(height: KSpace.md),
                Container(
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    color: c.elevated,
                    borderRadius: BorderRadius.circular(KRadius.md),
                  ),
                  child: Text(
                    'Enabled without verification — this environment has the identity checks switched off. Nothing about your identity has been confirmed.',
                    style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
                  ),
                ),
              ],
            ],
          ),
        ),
        _BottomAction(
          label: 'Continue',
          onPressed: publicBlocked || paidBlocked
              ? null
              : () => context.push(
                  '/events/create/form?product=$_product&pricing=$_pricing'),
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

/// One selectable answer. Mirrors web's `ChoiceCard` — and it is the ONLY card shape on this page, so
/// nothing on the gate looks tappable without being tappable (the defect D-343 removed).
class _ChoiceCard extends StatelessWidget {
  const _ChoiceCard({
    required this.selected,
    required this.icon,
    required this.title,
    required this.description,
    required this.onTap,
    this.note,
    this.disabled = false,
  });
  final bool selected;
  final IconData icon;
  final String title;
  final String description;
  final VoidCallback onTap;
  final String? note;
  final bool disabled;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Semantics(
      selected: selected,
      button: true,
      enabled: !disabled,
      child: Opacity(
        opacity: disabled ? 0.5 : 1,
        child: InkWell(
          // Selectable while unverified, deliberately: choosing it is how the person SEES what is
          // missing. Continue is what refuses, not the card. `disabled` is only for Paid-on-Private,
          // which is impossible rather than merely unverified.
          onTap: disabled ? null : onTap,
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
                    if (note != null) ...[
                      const SizedBox(width: KSpace.sm),
                      Flexible(
                        child: Text('· $note',
                            style: TextStyle(color: c.muted, fontSize: 12),
                            overflow: TextOverflow.ellipsis),
                      ),
                    ],
                  ],
                ),
                const SizedBox(height: KSpace.xs),
                Text(description, style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35)),
              ],
            ),
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
