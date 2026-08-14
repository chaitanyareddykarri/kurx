import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/theme/motion.dart';

/// Invitation RSVP — opened from a deep-link or notification.
class InvitationRsvpPage extends ConsumerStatefulWidget {
  const InvitationRsvpPage({super.key, required this.invitationId});
  final String invitationId;

  @override
  ConsumerState<InvitationRsvpPage> createState() =>
      _InvitationRsvpPageState();
}

class _InvitationRsvpPageState extends ConsumerState<InvitationRsvpPage> {
  _Rsvp? _selected;
  bool _loading = false;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Invitation')),
      body: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Inviter card
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
                    const KurxAvatar(name: 'Friend', size: 64),
                    const SizedBox(height: KSpace.md),
                    Text(
                      'You were invited!',
                      style: TextStyle(
                        color: c.text,
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: KSpace.xs),
                    Text(
                      'Someone invited you to join a group booking.',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                          color: c.muted, fontSize: 13.5, height: 1.4),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: KSpace.xl),
            Text(
              'Your response',
              style: TextStyle(
                color: c.text,
                fontWeight: FontWeight.w700,
                fontSize: 16,
              ),
            ),
            const SizedBox(height: KSpace.md),
            Row(
              children: [
                Expanded(
                  child: _RsvpButton(
                    label: 'Going',
                    icon: Icons.check_circle_rounded,
                    selected: _selected == _Rsvp.going,
                    color: c.success,
                    onTap: () => setState(() => _selected = _Rsvp.going),
                  ),
                ),
                const SizedBox(width: KSpace.sm),
                Expanded(
                  child: _RsvpButton(
                    label: 'Maybe',
                    icon: Icons.help_outline_rounded,
                    selected: _selected == _Rsvp.maybe,
                    color: c.warning,
                    onTap: () => setState(() => _selected = _Rsvp.maybe),
                  ),
                ),
                const SizedBox(width: KSpace.sm),
                Expanded(
                  child: _RsvpButton(
                    label: "Can't Go",
                    icon: Icons.cancel_outlined,
                    selected: _selected == _Rsvp.no,
                    color: c.danger,
                    onTap: () => setState(() => _selected = _Rsvp.no),
                  ),
                ),
              ],
            ),
            const Spacer(),
            KurxButton(
              label: _loading ? 'Confirming...' : 'Confirm RSVP',
              expand: true,
              onPressed: _selected == null || _loading ? null : _confirm,
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirm() async {
    setState(() => _loading = true);
    await Future.delayed(const Duration(milliseconds: 600));
    if (mounted) {
      setState(() => _loading = false);
      Navigator.of(context).pop();
    }
  }
}

enum _Rsvp { going, maybe, no }

class _RsvpButton extends StatelessWidget {
  const _RsvpButton({
    required this.label,
    required this.icon,
    required this.selected,
    required this.color,
    required this.onTap,
  });
  final String label;
  final IconData icon;
  final bool selected;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Semantics(
      button: true,
      selected: selected,
      child: GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: context.motion(KMotion.fast),
        padding: const EdgeInsets.symmetric(vertical: KSpace.md),
        decoration: BoxDecoration(
          color: selected ? color.withValues(alpha: 0.12) : c.elevated,
          borderRadius: BorderRadius.circular(KRadius.lg),
          border: Border.all(
            color: selected ? color : Colors.transparent,
            width: 1.5,
          ),
        ),
        child: Column(
          children: [
            Icon(icon, color: selected ? color : c.muted, size: 22),
            const SizedBox(height: KSpace.xs),
            Text(
              label,
              style: TextStyle(
                color: selected ? color : c.muted,
                fontSize: 12,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
      ),
    );
  }
}
