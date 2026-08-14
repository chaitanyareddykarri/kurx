import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/post.dart';

/// Media attached to a post. Layout follows count, not kind: 1 fills, 2+ becomes a 2-column grid,
/// and anything past the fourth image is collapsed into a "+n" on the last tile — so a 10-image
/// post stays roughly one screen tall instead of turning the feed into a scroll trap.
class PostMediaView extends StatelessWidget {
  const PostMediaView({super.key, required this.media, this.compact = false});

  final List<PostMedia> media;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final images = media.where((m) => m.isImage).toList();
    final videos = media.where((m) => m.isVideo).toList();
    final docs = media.where((m) => m.isDocument).toList();

    final shown = images.take(4).toList();
    final overflow = images.length - shown.length;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (shown.isNotEmpty)
          ClipRRect(
            borderRadius: BorderRadius.circular(KRadius.md),
            child: shown.length == 1
                ? _Tile(media: shown.first, height: compact ? 140 : 260)
                : GridView.count(
                    crossAxisCount: 2,
                    shrinkWrap: true,
                    physics: const NeverScrollableScrollPhysics(),
                    mainAxisSpacing: 2,
                    crossAxisSpacing: 2,
                    childAspectRatio: 1.4,
                    children: [
                      for (var i = 0; i < shown.length; i++)
                        _Tile(
                          media: shown[i],
                          overflow: i == shown.length - 1 && overflow > 0 ? overflow : null,
                        ),
                    ],
                  ),
          ),
        // Video playback needs a player package this app does not ship; a tappable poster is the
        // honest surface until one is chosen, rather than a control that does nothing.
        for (final v in videos) ...[
          const SizedBox(height: KSpace.sm),
          _DocumentRow(icon: Icons.play_circle_outline_rounded, label: 'Video', media: v),
        ],
        for (final d in docs) ...[
          const SizedBox(height: KSpace.sm),
          _DocumentRow(icon: Icons.description_outlined, label: d.kind.isEmpty ? 'Document' : d.kind, media: d),
        ],
      ],
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.media, this.height, this.overflow});

  final PostMedia media;
  final double? height;
  final int? overflow;

  @override
  Widget build(BuildContext context) {
    final image = Image.network(
      media.url,
      height: height,
      width: double.infinity,
      fit: BoxFit.cover,
      errorBuilder: (_, _, _) => Container(
        height: height ?? 120,
        color: context.kurx.elevated,
        child: Icon(Icons.broken_image_outlined, color: context.kurx.muted),
      ),
    );

    if (overflow == null) return image;
    return Stack(
      fit: StackFit.passthrough,
      children: [
        image,
        Positioned.fill(
          child: ColoredBox(
            color: Colors.black54,
            child: Center(
              child: Text(
                '+$overflow',
                style: const TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.w700),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _DocumentRow extends StatelessWidget {
  const _DocumentRow({required this.icon, required this.label, required this.media});

  final IconData icon;
  final String label;
  final PostMedia media;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        border: Border.all(color: c.border),
        borderRadius: BorderRadius.circular(KRadius.md),
      ),
      child: Row(
        children: [
          Icon(icon, size: 18, color: c.muted),
          const SizedBox(width: KSpace.sm),
          Expanded(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: c.text),
            ),
          ),
          Text(
            '${(media.sizeBytes / 1024).round()} KB',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
          ),
        ],
      ),
    );
  }
}
