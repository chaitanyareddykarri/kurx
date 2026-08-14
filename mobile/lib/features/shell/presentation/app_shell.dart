import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/design_tokens.dart';
import '../../../core/theme/motion.dart';

/// The persistent app shell wrapping the 5-tab floating pill nav (D-066 — the
/// original comment cited D-057, which is the admin event-approval queue).
///
/// Tabs are the product flow's Main Application areas: **Home / Community / Posts / Messages /
/// Workspace**. Profile sits top-left and Notifications top-right in [KurxShellAppBar] — the flow
/// names seven areas and a pill nav holds five, and those two are the ones you visit and come back
/// from rather than dwell in, so they get fixed corners reachable from every tab instead of a slot.
///
/// Browse / Tickets / Saved are no longer tabs; they sit under Home and Profile, matching the
/// flow's own nesting.
class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  /// The bar's height at a given text scale — see [_navBarHeight].
  @visibleForTesting
  static double navBarHeightFor(TextScaler scaler) => _navBarHeight(scaler);

  @override
  Widget build(BuildContext context) {
    // The reservation below the content used to be a hardcoded 80px. That is a
    // guess about three things it cannot see: the bar's own height, the device's
    // bottom safe-area inset, and the user's text scale. At large text sizes or
    // on a device with a taller home indicator the last row of every list sat
    // underneath the bar. Measuring all three instead means the reservation is
    // correct on any device rather than on the one it was tuned against.
    final media = MediaQuery.of(context);
    final barHeight = _navBarHeight(media.textScaler);
    final reserved = barHeight + media.padding.bottom + KSpace.md;

    return Scaffold(
      body: Stack(
        children: [
          Positioned.fill(
            child: Padding(
              padding: EdgeInsets.only(bottom: reserved),
              child: navigationShell,
            ),
          ),
          Positioned(
            left: 0, right: 0, bottom: 0,
            child: _KurxNavBar(
              currentIndex: navigationShell.currentIndex,
              onTap: (i) => navigationShell.goBranch(
                i,
                initialLocation: i == navigationShell.currentIndex,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Floating pill nav bar (D-057)
// ─────────────────────────────────────────────────────────────────────────────

typedef _NavItem = ({IconData outline, IconData filled, String label});

const List<_NavItem> _navItems = [
  (outline: Icons.home_outlined,                  filled: Icons.home_rounded,                  label: 'Home'),
  (outline: Icons.people_outline_rounded,         filled: Icons.people_rounded,                label: 'Community'),
  (outline: Icons.article_outlined,               filled: Icons.article_rounded,               label: 'Posts'),
  (outline: Icons.forum_outlined,                 filled: Icons.forum_rounded,                 label: 'Messages'),
  (outline: Icons.dashboard_outlined,             filled: Icons.dashboard_rounded,             label: 'Workspace'),
];

class _KurxNavBar extends StatelessWidget {
  const _KurxNavBar({required this.currentIndex, required this.onTap});

  final int currentIndex;
  final ValueChanged<int> onTap;

  @override
  Widget build(BuildContext context) {
    final c   = context.kurx;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(KSpace.lg, 0, KSpace.lg, KSpace.md),
        child: DecoratedBox(
          decoration: BoxDecoration(
            color: isDark ? c.cardSurface : Colors.white,
            borderRadius: BorderRadius.circular(KRadius.pill),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: isDark ? 0.32 : 0.10),
                blurRadius: 24,
                offset: const Offset(0, 8),
              ),
              BoxShadow(
                color: Colors.black.withValues(alpha: isDark ? 0.16 : 0.04),
                blurRadius: 6,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: KSpace.sm + 2),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceAround,
              children: List.generate(_navItems.length, (i) {
                final item     = _navItems[i];
                final selected = i == currentIndex;
                // Was a bare GestureDetector: not announced as a control at all,
                // with no selected state and no minimum tap target. A tab bar is
                // the most-used control in the app and was invisible to
                // TalkBack/VoiceOver as anything actionable.
                return Semantics(
                  label: item.label,
                  button: true,
                  selected: selected,
                  child: InkWell(
                    onTap: () => onTap(i),
                    customBorder: const StadiumBorder(),
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(minWidth: 48, minHeight: 48),
                      child: Center(
                        child: AnimatedContainer(
                    duration: context.motion(KMotion.base),
                    curve:    context.motionCurve,
                    padding: EdgeInsets.symmetric(
                      horizontal: selected ? KSpace.lg : KSpace.md,
                      vertical:   KSpace.sm + 2,
                    ),
                    decoration: selected
                        ? BoxDecoration(
                            color:        c.accent,
                            borderRadius: BorderRadius.circular(KRadius.pill),
                          )
                        : null,
                    // No badge here: the notification bell lives in the app bar (KurxShellAppBar),
                    // so an unread count in the nav would be a second, drifting copy of it.
                    child: _NavIcon(
                      icon:  selected ? item.filled : item.outline,
                      color: selected ? c.onAccent : c.muted,
                    ),
                        ),
                      ),
                    ),
                  ),
                );
              }),
            ),
          ),
        ),
      ),
    );
  }
}

/// The pill bar's intrinsic height: icon + label + the vertical padding around
/// them, scaled by the user's text-size preference.
///
/// Exposed (via [AppShell.navBarHeightFor]) so a test can assert the content
/// reservation tracks it rather than a constant.
double _navBarHeight(TextScaler scaler) {
  const iconSize = 24.0;
  const labelSize = 11.0;
  const verticalPadding = (KSpace.sm + 2) * 2; // the pill's own padding
  const outerPadding = KSpace.md; // gap below the bar
  return iconSize + scaler.scale(labelSize) + verticalPadding + outerPadding;
}

class _NavIcon extends StatelessWidget {
  const _NavIcon({required this.icon, required this.color});

  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) => Icon(icon, color: color, size: 24);
}
