import 'package:flutter/material.dart';

import 'design_tokens.dart';

/// Reduced-motion support.
///
/// Web gets this free from a global `prefers-reduced-motion` block in
/// `packages/ui/src/styles/tokens.css`, which clamps every animation including
/// components not yet redesigned. Flutter has no equivalent global hook, and
/// before this there were **zero** references to `disableAnimations` or
/// `accessibleNavigation` anywhere in `mobile/lib` — so a user who had asked the
/// OS to reduce motion still got every transition (WCAG 2.3.3).
///
/// `docs/ui-ux/accessibility-foundation.md` §5 draws the distinction this
/// implements: clamping a duration is enough for a fade, but **not** for a
/// positional move. A slide that merely goes faster is still a slide.
extension KurxMotion on BuildContext {
  /// True when the platform asks for reduced motion.
  bool get reduceMotion => MediaQuery.maybeDisableAnimationsOf(this) ?? false;

  /// A duration that collapses to near-zero under reduced motion.
  Duration motion(Duration full) => reduceMotion ? const Duration(milliseconds: 1) : full;

  /// The standard curve, or a linear one when movement is suppressed.
  Curve get motionCurve => reduceMotion ? Curves.linear : KMotion.curve;

  /// An offset that resolves to "no movement" under reduced motion, so a
  /// slide-in becomes a plain fade rather than a fast slide.
  Offset slideFrom(Offset offset) => reduceMotion ? Offset.zero : offset;
}
