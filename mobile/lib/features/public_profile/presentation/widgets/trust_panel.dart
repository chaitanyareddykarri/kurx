import 'package:flutter/material.dart';

import '../../../../common/widgets/kurx_card.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/public_profile_dto.dart';

/// The public Trust panel — the Flutter twin of `web/components/profile/trust-panel.tsx`.
///
/// **This is deliberately not the verification centre.** D-221 fixes the public profile to
/// *positive, verified signals only*: a badge appears when something was proved and is simply absent
/// otherwise. There is no "pending", no "rejected", and no PAN / bank / penny-drop state anywhere on
/// this surface — those are private facts about a person's financial identity, and publishing their
/// *status* leaks them as surely as publishing their value would. "PAN rejected" on a public page is
/// a defamatory-by-default UI.
///
/// The owner's full verification state lives behind auth in Settings, which is the only place it
/// belongs. Absence here is therefore never a negative claim: a profile with one badge is not
/// "unverified", it is a profile that has proved one thing.
class TrustPanel extends StatelessWidget {
  const TrustPanel({super.key, required this.verification});

  final VerificationBadgesDto verification;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final v = verification;

    // The publication rule: a badge appears only if it answers "can a stranger rely on this person
    // to run or take part in an event" AND revealing it discloses nothing they would not volunteer.
    //
    // Email verified failed both halves — it answers "we can reach them", not "they are
    // trustworthy", and publishing it tells a stranger an address is on file. Phone was never here
    // and never should be: every account exists only after an OTP login, so the badge is true for
    // everyone and carries no information. Organization / speaker / organizer were removed at the
    // product owner's direction. What remains is who they are, what they earned, and how long they
    // have been here.
    final signals = <({IconData icon, String label, String detail})>[
      if (v.identityVerified)
        (icon: Icons.verified_user_outlined, label: 'Identity verified', detail: 'Government ID confirmed'),
      // Guarded on > 0, not on non-null: "0 verified certificates" is not a trust signal, and null
      // means hidden from this viewer rather than a count (D-229).
      if (v.verifiedCertificates != null && v.verifiedCertificates! > 0)
        (
          icon: Icons.workspace_premium_outlined,
          label: '${v.verifiedCertificates} verified '
              '${v.verifiedCertificates == 1 ? 'certificate' : 'certificates'}',
          detail: 'Each independently verifiable by code',
        ),
      if (v.yearsOnPlatform >= 1)
        (
          icon: Icons.schedule_outlined,
          label: '${v.yearsOnPlatform}+ ${v.yearsOnPlatform == 1 ? 'year' : 'years'} on Kurx',
          detail: 'Account age',
        ),
    ];


    // Nothing proved yet renders nothing at all. An empty "Trust" card would say "this person has
    // verified nothing", which is a claim the platform has no business making on their page.
    if (signals.isEmpty) return const SizedBox.shrink();

    return KurxCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Trust',
            style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: KSpace.xs),
          Text(
            'Every signal here was proved by an independent record. Nothing on this list is self-declared.',
            style: TextStyle(color: c.muted, fontSize: 13, height: 1.4),
          ),
          const SizedBox(height: KSpace.lg),
          for (final s in signals) ...[
            Container(
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
                  Icon(s.icon, size: 18, color: c.teal),
                  const SizedBox(width: KSpace.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          s.label,
                          style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w600),
                        ),
                        const SizedBox(height: 2),
                        Text(s.detail, style: TextStyle(color: c.muted, fontSize: 12.5)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}
