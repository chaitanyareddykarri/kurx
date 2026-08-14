import 'package:flutter/material.dart';

import '../../core/theme/design_tokens.dart';

/// A circular avatar that falls back to initials on a deterministic tint when no
/// image is available — so seeded users always render something intentional,
/// never a broken image icon.
class KurxAvatar extends StatelessWidget {
  const KurxAvatar({super.key, required this.name, this.imageUrl, this.size = 40});

  final String name;
  final String? imageUrl;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    if (imageUrl != null && imageUrl!.isNotEmpty) {
      return CircleAvatar(radius: size / 2, backgroundImage: NetworkImage(imageUrl!));
    }
    // Deterministic hue from the name keeps a person's color stable across screens.
    final hue = (name.codeUnits.fold<int>(0, (a, b) => a + b) * 37) % 360;
    final tint = HSLColor.fromAHSL(1, hue.toDouble(), 0.5, 0.5).toColor();
    return Container(
      height: size,
      width: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: tint.withValues(alpha: 0.2),
        border: Border.all(color: c.border),
      ),
      alignment: Alignment.center,
      child: Text(
        _initials(name),
        style: TextStyle(color: c.text, fontSize: size * 0.36, fontWeight: FontWeight.w700),
      ),
    );
  }

  static String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.characters.first.toUpperCase();
    return (parts.first.characters.first + parts.last.characters.first).toUpperCase();
  }
}
