import 'package:flutter/material.dart';

/// Makes a centered message fill (and scroll within) the viewport, so a surrounding
/// RefreshIndicator can still be pulled on empty/error states.
class FillViewport extends StatelessWidget {
  const FillViewport({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight.isFinite ? constraints.maxHeight : 0),
          child: child,
        ),
      ),
    );
  }
}
