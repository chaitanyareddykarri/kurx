import 'package:flutter/material.dart';

import '../../../../common/widgets/kurx_badge.dart';
import '../../../../common/widgets/kurx_card.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../orders/data/models/attendee_dtos.dart';

/// Component state on the verification ladder. `notStarted` and `rejected` are deliberately
/// different: one is work not begun, the other is work that came back and needs a person to act.
enum VerificationState { verified, pending, rejected, notStarted }

/// Maps a server component status onto the four display states.
///
/// **This used to infer state from whether a masked value was present**, because per-component
/// status did not exist on the wire — so "submitted and rejected" and "never attempted" rendered
/// identically. The server now reports each component and this only translates vocabulary. Mirrors
/// `web/components/profile/verification-section.tsx` exactly: the two must never disagree about what
/// "verified" means, because the same server flags gate the money path either way.
VerificationState verificationStateOf(String status) => switch (status.toLowerCase()) {
      'approved' => VerificationState.verified,
      'rejected' || 'changesrequested' || 'expired' || 'revoked' => VerificationState.rejected,
      'submitted' || 'underreview' => VerificationState.pending,
      _ => VerificationState.notStarted,
    };

/// Penny drop has its own vocabulary — it is a bank test, not a review decision.
VerificationState pennyDropStateOf(String status) => switch (status.toLowerCase()) {
      'passed' => VerificationState.verified,
      'failed' => VerificationState.rejected,
      'pending' => VerificationState.pending,
      _ => VerificationState.notStarted,
    };

({String label, KurxBadgeTone tone, IconData icon}) _ui(VerificationState s) => switch (s) {
      VerificationState.verified =>
        (label: 'Verified', tone: KurxBadgeTone.teal, icon: Icons.check_circle_outline_rounded),
      VerificationState.pending =>
        (label: 'Pending', tone: KurxBadgeTone.warning, icon: Icons.schedule_rounded),
      VerificationState.rejected =>
        (label: 'Needs action', tone: KurxBadgeTone.danger, icon: Icons.error_outline_rounded),
      VerificationState.notStarted =>
        (label: 'Not started', tone: KurxBadgeTone.muted, icon: Icons.circle_outlined),
    };

/// Verification — the **owner-only** block on a profile. The Flutter twin of
/// `web/components/profile/verification-section.tsx`.
///
/// **Who sees this is the whole design.** The public half of verification is a short list of proved
/// signals (see `TrustPanel`); this is everything that half deliberately withholds — which
/// components are pending, which were rejected, and what each unlocks. It renders only for the
/// profile's owner, because a component's *status* is as private as its value.
///
/// **The money model is user-level.** A person's own identity and bank verification unlock that
/// person's wallet and payouts; an organization verifies the *user for events* and holds nothing.
class VerificationSection extends StatelessWidget {
  const VerificationSection({
    super.key,
    required this.identity,
    required this.history,
    required this.emailVerified,
    required this.canOrganizePaid,
    required this.canReceivePayout,
    required this.onVerifyEmail,
    required this.onVerifyIdentity,
  });

  /// Null when the read failed — the section still renders rather than vanishing, because a person
  /// locked out of selling tickets is exactly who needs it on screen.
  final IdentityStatusDto? identity;

  /// Newest first. Empty when nothing was ever submitted — an empty trail, not an error.
  final List<IdentityHistoryEntryDto> history;
  final bool emailVerified;
  final bool canOrganizePaid;
  final bool canReceivePayout;
  final VoidCallback onVerifyEmail;
  final VoidCallback onVerifyIdentity;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final govt = verificationStateOf(identity?.govtIdStatus ?? 'NotStarted');
    final pan = verificationStateOf(identity?.panStatus ?? 'NotStarted');
    final bank = verificationStateOf(identity?.bankStatus ?? 'NotStarted');
    final pennyDrop = pennyDropStateOf(identity?.pennyDropStatus ?? 'NotStarted');
    final nameMatch = (identity?.bankNameMatch ?? 'NotChecked').toLowerCase();
    final identityVerified =
        govt == VerificationState.verified || pan == VerificationState.verified;

    return Column(
      children: [
        KurxCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      'Verification',
                      style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w700),
                    ),
                  ),
                  if (identityVerified)
                    const KurxBadge(
                      label: 'Identity verified',
                      tone: KurxBadgeTone.teal,
                      icon: Icons.verified_user_outlined,
                    ),
                ],
              ),
              const SizedBox(height: KSpace.xs),
              Text(
                'Only you can see this. Only the last four digits of anything you submit are stored — '
                'the full value goes to the verification provider and is never kept by Kurx.',
                style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.45),
              ),
              const SizedBox(height: KSpace.lg),

              // True by construction: an account only exists after a successful OTP login, which is
              // why the backend states it rather than storing a redundant column.
              const _Row(
                icon: Icons.phone_iphone_rounded,
                label: 'Phone',
                description: 'Verified when you signed in — every account is created by a one-time code.',
                state: VerificationState.verified,
              ),
              _Row(
                icon: Icons.mail_outline_rounded,
                label: 'Email',
                description: emailVerified
                    ? 'Confirmed by one-time code.'
                    : 'Used for recovery and receipts.',
                state: emailVerified ? VerificationState.verified : VerificationState.notStarted,
                actionLabel: 'Verify',
                onAction: onVerifyEmail,
              ),
              _Row(
                icon: Icons.badge_outlined,
                label: 'Government ID',
                description: identity?.govtIdLast4 != null
                    ? 'Confirmed · ending ${identity!.govtIdLast4}'
                    : 'DigiLocker, Aadhaar offline, passport or driving licence.',
                state: govt,
                actionLabel: 'Verify',
                onAction: onVerifyIdentity,
              ),
              _Row(
                icon: Icons.credit_card_outlined,
                label: 'PAN',
                description: identity?.panLast4 != null
                    ? 'Confirmed · ending ${identity!.panLast4}'
                    : 'Required to take payments.',
                state: pan,
                actionLabel: 'Verify',
                onAction: onVerifyIdentity,
              ),
              _Row(
                icon: Icons.account_balance_outlined,
                label: 'Bank account',
                description: identity?.bankLast4 != null
                    ? 'Account ending ${identity!.bankLast4}'
                    : 'Where your payouts are settled.',
                state: bank,
                actionLabel: 'Add',
                onAction: onVerifyIdentity,
              ),
              _Row(
                icon: Icons.savings_outlined,
                label: 'Penny drop',
                description: switch (pennyDrop) {
                  VerificationState.verified =>
                    'A ₹1 credit reached the account — it exists and accepts deposits.',
                  VerificationState.rejected =>
                    'The test credit did not land. Check the account number and IFSC.',
                  _ => 'A ₹1 test credit proves the account is real and live.',
                },
                state: pennyDrop,
                actionLabel: 'Retry',
                onAction: onVerifyIdentity,
              ),
              _Row(
                icon: Icons.how_to_reg_outlined,
                label: 'Account holder',
                description: switch (nameMatch) {
                  'match' => "The bank's registered holder name matched yours.",
                  'partialmatch' => 'The holder name partly matched — a review may be needed.',
                  'mismatch' =>
                    'The holder name did not match. Payouts must settle to an account in your name.',
                  _ => "Checked against the bank's registered holder name when you add an account.",
                },
                state: switch (nameMatch) {
                  'match' => VerificationState.verified,
                  'mismatch' => VerificationState.rejected,
                  'partialmatch' => VerificationState.pending,
                  _ => VerificationState.notStarted,
                },
                actionLabel: 'Fix',
                onAction: onVerifyIdentity,
              ),
            ],
          ),
        ),
        const SizedBox(height: KSpace.lg),
        KurxCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'What this unlocks',
                style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: KSpace.xs),
              Text(
                'Checked live on every request — a change takes effect on your next action, not your '
                'next sign-in.',
                style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.45),
              ),
              const SizedBox(height: KSpace.lg),
              const _Capability(
                icon: Icons.check_circle_outline_rounded,
                label: 'Attend events, chat, post, run free events',
                unlocked: true,
                requirement: 'Available to every account.',
              ),
              _Capability(
                icon: Icons.verified_user_outlined,
                label: 'Create and sell paid events',
                unlocked: canOrganizePaid,
                requirement: 'Needs your identity and your bank account verified.',
              ),
              _Capability(
                icon: Icons.account_balance_wallet_outlined,
                label: 'Your wallet and payouts',
                unlocked: canReceivePayout,
                requirement: 'Needs your bank account verified. Earnings settle to you.',
              ),
              if (!canOrganizePaid) ...[
                const SizedBox(height: KSpace.md),
                Container(
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    color: c.elevated,
                    borderRadius: BorderRadius.circular(KRadius.md),
                    border: Border.all(color: c.warning.withValues(alpha: 0.4)),
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(Icons.warning_amber_rounded, size: 16, color: c.warning),
                      const SizedBox(width: KSpace.sm),
                      Expanded(
                        child: Text(
                          'Until this is complete, buyers cannot pay for tickets on your events — '
                          'checkout refuses the payment rather than failing quietly.',
                          style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.4),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ],
          ),
        ),
        if (history.isNotEmpty) ...[
          const SizedBox(height: KSpace.lg),
          KurxCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(Icons.history_rounded, size: 18, color: c.muted),
                    const SizedBox(width: KSpace.sm),
                    Text(
                      'Verification history',
                      style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w700),
                    ),
                  ],
                ),
                const SizedBox(height: KSpace.xs),
                Text(
                  'Every decision made about your identity, newest first.',
                  style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.45),
                ),
                const SizedBox(height: KSpace.lg),
                for (final h in history) _HistoryRow(entry: h),
              ],
            ),
          ),
        ],
      ],
    );
  }
}

class _HistoryRow extends StatelessWidget {
  const _HistoryRow({required this.entry});

  final IdentityHistoryEntryDto entry;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final approved = entry.decision == 'approve' || entry.decision == 'approved';
    final label = entry.component.replaceAll('_', ' ');

    return Container(
      margin: const EdgeInsets.only(bottom: KSpace.md),
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        color: c.elevated,
        borderRadius: BorderRadius.circular(KRadius.md),
        border: Border.all(color: c.border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  spacing: KSpace.sm,
                  runSpacing: KSpace.xs,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(
                      label.isEmpty
                          ? 'Unknown'
                          : '${label[0].toUpperCase()}${label.substring(1)}',
                      style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w600),
                    ),
                    KurxBadge(
                      label: approved ? 'Approved' : 'Rejected',
                      tone: approved ? KurxBadgeTone.teal : KurxBadgeTone.danger,
                      icon: approved
                          ? Icons.check_circle_outline_rounded
                          : Icons.cancel_outlined,
                    ),
                  ],
                ),
                if (entry.notes != null && entry.notes!.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(entry.notes!, style: TextStyle(color: c.muted, fontSize: 12.5)),
                ],
              ],
            ),
          ),
          const SizedBox(width: KSpace.sm),
          Text(
            '${entry.createdAt.day}/${entry.createdAt.month}/${entry.createdAt.year}',
            style: TextStyle(color: c.muted, fontSize: 12.5),
          ),
        ],
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({
    required this.icon,
    required this.label,
    required this.description,
    required this.state,
    this.actionLabel,
    this.onAction,
  });

  final IconData icon;
  final String label;
  final String description;
  final VerificationState state;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final ui = _ui(state);

    return Container(
      margin: const EdgeInsets.only(bottom: KSpace.md),
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        color: c.elevated,
        borderRadius: BorderRadius.circular(KRadius.md),
        border: Border.all(color: c.border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 17, color: c.muted),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  spacing: KSpace.sm,
                  runSpacing: KSpace.xs,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(
                      label,
                      style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w600),
                    ),
                    KurxBadge(label: ui.label, tone: ui.tone, icon: ui.icon),
                  ],
                ),
                const SizedBox(height: 2),
                Text(description, style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35)),
              ],
            ),
          ),
          // A verified component offers no action: there is nothing left to do, and a live button
          // invites a resubmission that would only reset a passed check.
          if (state != VerificationState.verified && actionLabel != null && onAction != null) ...[
            const SizedBox(width: KSpace.sm),
            TextButton(onPressed: onAction, child: Text(actionLabel!)),
          ],
        ],
      ),
    );
  }
}

class _Capability extends StatelessWidget {
  const _Capability({
    required this.icon,
    required this.label,
    required this.unlocked,
    required this.requirement,
  });

  final IconData icon;
  final String label;
  final bool unlocked;
  final String requirement;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.md),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 17, color: unlocked ? c.teal : c.muted),
          const SizedBox(width: KSpace.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 2),
                // Shown whether unlocked or not: someone who has it should still see what keeps it.
                Text(requirement, style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35)),
              ],
            ),
          ),
          const SizedBox(width: KSpace.sm),
          KurxBadge(
            label: unlocked ? 'Unlocked' : 'Locked',
            tone: unlocked ? KurxBadgeTone.teal : KurxBadgeTone.muted,
            icon: unlocked ? Icons.check_circle_outline_rounded : Icons.lock_outline_rounded,
          ),
        ],
      ),
    );
  }
}
