import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/theme/motion.dart';
import '../providers/chat_providers.dart';

/// An avatar with an online dot (D-114).
///
/// The dot is drawn only when presence is actually known. When the server has no shared presence
/// store the indicator is absent entirely rather than showing everyone as offline — "we cannot know"
/// and "nobody is here" must not look the same.
class PresenceAvatar extends StatelessWidget {
  const PresenceAvatar({
    super.key,
    required this.name,
    required this.size,
    required this.online,
    required this.presenceKnown,
  });

  final String name;
  final double size;
  final bool online;
  final bool presenceKnown;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final avatar = KurxAvatar(name: name, size: size);
    if (!presenceKnown) return avatar;

    final dot = size * 0.3;
    return Semantics(
      // Announced as words, never colour alone.
      label: online ? '$name, online' : '$name, offline',
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          ExcludeSemantics(child: avatar),
          Positioned(
            right: -1,
            bottom: -1,
            child: Container(
              width: dot,
              height: dot,
              decoration: BoxDecoration(
                color: online ? c.success : c.muted,
                shape: BoxShape.circle,
                // Ring in the surface colour so the dot reads against any avatar tint, which is
                // what keeps contrast acceptable in both themes.
                border: Border.all(color: c.background, width: 1.5),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// "Alice is typing…" above the composer.
///
/// A `ConsumerWidget` watching **only** the typing slice: a keystroke from another member rebuilds
/// this banner and nothing else, so the message list is untouched while someone types.
class TypingBanner extends ConsumerWidget {
  const TypingBanner({super.key, required this.roomId, required this.myUserId});

  final String roomId;
  final String? myUserId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final typists = ref.watch(
      chatRoomControllerProvider(roomId).select((s) => s.typistsExcept(myUserId)),
    );
    if (typists.isEmpty) return const SizedBox.shrink();

    final c = context.kurx;
    final label = switch (typists.length) {
      1 => '${typists.first.name} is typing…',
      2 => '${typists[0].name} and ${typists[1].name} are typing…',
      // Overflow: naming everyone would wrap and jitter as the set changes.
      _ => '${typists.first.name} and ${typists.length - 1} others are typing…',
    };

    return Semantics(
      liveRegion: true,
      label: label,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(KSpace.lg, 0, KSpace.lg, KSpace.xs),
        child: Row(
          children: [
            const _TypingDots(),
            const SizedBox(width: KSpace.sm),
            Expanded(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(color: c.muted, fontSize: 12, fontStyle: FontStyle.italic),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Three dots pulsing in sequence. Excluded from semantics — the banner already announces the text,
/// and an animation has nothing useful to say to a screen reader.
class _TypingDots extends StatefulWidget {
  const _TypingDots();

  @override
  State<_TypingDots> createState() => _TypingDotsState();
}

class _TypingDotsState extends State<_TypingDots> with SingleTickerProviderStateMixin {
  late final AnimationController _controller =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 1100));

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Typing dots repeat for as long as the other person is typing — no fixed end, so the same
    // WCAG 2.2.2 reasoning as the skeleton applies.
    if (context.reduceMotion) {
      _controller.stop();
    } else if (!_controller.isAnimating) {
      _controller.repeat();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return ExcludeSemantics(
      child: AnimatedBuilder(
        animation: _controller,
        builder: (context, _) => Row(
          mainAxisSize: MainAxisSize.min,
          children: List.generate(3, (i) {
            // Each dot leads the next by a third of the cycle.
            final t = (_controller.value + i * 0.33) % 1.0;
            // Frozen dots read as three dots, not as a half-finished animation.
            final opacity =
                context.reduceMotion ? 0.7 : 0.3 + 0.7 * (t < 0.5 ? t * 2 : (1 - t) * 2);
            return Padding(
              padding: const EdgeInsets.symmetric(horizontal: 1.5),
              child: Container(
                width: 5,
                height: 5,
                decoration: BoxDecoration(
                  color: c.muted.withValues(alpha: opacity),
                  shape: BoxShape.circle,
                ),
              ),
            );
          }),
        ),
      ),
    );
  }
}
