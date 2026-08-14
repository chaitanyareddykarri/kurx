import 'package:flutter/material.dart';

/// Constrains content to a comfortable reading width and centres it, so single-column
/// screens don't stretch edge-to-edge on tablets/large windows.
class ContentWidth extends StatelessWidget {
  const ContentWidth({super.key, required this.child, this.maxWidth = 720});

  final Widget child;
  final double maxWidth;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: maxWidth),
        child: child,
      ),
    );
  }
}

/// Whether the current width warrants tablet/large layouts.
bool isWideScreen(BuildContext context) => MediaQuery.sizeOf(context).width >= 720;
