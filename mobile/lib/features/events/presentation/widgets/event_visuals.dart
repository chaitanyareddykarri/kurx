import 'package:flutter/material.dart';

/// Deterministic, network-free imagery for events. A seed (the slug) maps to a
/// stable gradient so a given event always looks the same, and the category maps
/// to an icon. This gives premium hero art without any image assets or network.
abstract final class EventVisuals {
  /// Blue-family gradients (D-288, replacing D-286's warm ember set).
  ///
  /// This art backs every event without an uploaded image, so it is the
  /// most-seen colour in the app; it has to belong to the palette rather than
  /// be a general-purpose spread. These run blue → indigo → sky → teal →
  /// violet, anchored on the brand's own hue, with the dark stop of each pair
  /// resolving toward the near-black page background.
  ///
  /// Teal and violet carry no semantic weight here — this is decorative art,
  /// not a status.
  ///
  /// Every stop clears **3:1 against white**, because the callers draw a white
  /// icon on top of these tiles and WCAG 1.4.11 governs meaningful non-text
  /// content. The lightest stop used, `#2563EB`, is 5.17:1.
  static const List<List<Color>> _gradients = [
    [Color(0xFF2563EB), Color(0xFF1E3A8A)], // blue → navy
    [Color(0xFF1D4ED8), Color(0xFF312E81)], // blue → deep indigo
    [Color(0xFF4F46E5), Color(0xFF3730A3)], // indigo → deep indigo
    [Color(0xFF0284C7), Color(0xFF075985)], // sky → deep sky
    [Color(0xFF0F766E), Color(0xFF134E4A)], // teal → deep teal
    [Color(0xFF7C3AED), Color(0xFF4C1D95)], // violet → deep violet
    [Color(0xFF155E75), Color(0xFF0F172A)], // cyan → slate ink
    [Color(0xFF2563EB), Color(0xFF1E293B)], // blue → slate
  ];

  /// Exposed for the contrast guard in `test/core/design_tokens_test.dart`.
  static List<List<Color>> get allGradients => _gradients;

  static List<Color> gradientFor(String seed) {
    final hash = seed.codeUnits.fold<int>(0, (a, b) => (a * 31 + b) & 0x7fffffff);
    return _gradients[hash % _gradients.length];
  }

  static LinearGradient linearFor(String seed) => LinearGradient(
        colors: gradientFor(seed),
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
      );

  static IconData iconFor(String? category) {
    switch (category?.toLowerCase()) {
      case 'music':
        return Icons.music_note_rounded;
      case 'comedy':
        return Icons.theater_comedy_rounded;
      case 'workshops':
        return Icons.build_circle_outlined;
      case 'tech':
        return Icons.memory_rounded;
      case 'sports':
        return Icons.directions_run_rounded;
      case 'college fests':
        return Icons.school_rounded;
      case 'arts & theatre':
        return Icons.palette_rounded;
      case 'food & drink':
        return Icons.restaurant_rounded;
      case 'business':
        return Icons.trending_up_rounded;
      case 'wellness':
        return Icons.self_improvement_rounded;
      case 'gaming':
        return Icons.sports_esports_rounded;
      case 'film':
        return Icons.movie_rounded;
      case 'community':
        return Icons.groups_rounded;
      default:
        return Icons.event_rounded;
    }
  }
}

/// A gradient hero panel with the category glyph watermarked in — the shared
/// visual header for cards and the detail page.
class EventHero extends StatelessWidget {
  const EventHero({
    super.key,
    required this.seed,
    this.category,
    this.height,
    this.borderRadius = BorderRadius.zero,
    this.overlay,
    this.imageUrl,
  });

  final String seed;
  final String? category;
  final double? height;
  final BorderRadius borderRadius;
  final Widget? overlay;

  /// The event's real banner, presigned. When absent the deterministic gradient stands in — it always
  /// did, which is why no Flutter surface ever showed an actual event image (D-302).
  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: borderRadius,
      child: SizedBox(
        height: height,
        child: DecoratedBox(
          decoration: BoxDecoration(gradient: EventVisuals.linearFor(seed)),
          child: Stack(
            fit: StackFit.expand,
            children: [
              // Real banner over the gradient. A failed load falls back to the gradient rather than a
              // broken-image glyph, so a dead storage key degrades instead of looking broken.
              if (imageUrl != null && imageUrl!.isNotEmpty)
                Image.network(
                  imageUrl!,
                  fit: BoxFit.cover,
                  errorBuilder: (_, _, _) => const SizedBox.shrink(),
                ),
              Positioned(
                right: -12,
                bottom: -12,
                child: Icon(
                  EventVisuals.iconFor(category),
                  size: 120,
                  color: Colors.white.withValues(alpha: 0.16),
                ),
              ),
              ?overlay,
            ],
          ),
        ),
      ),
    );
  }
}
