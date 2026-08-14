import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/kurx_card.dart';
import '../../../../core/theme/design_tokens.dart';
// `relativeTime` is a pure helper that happens to live under auth. Imported rather than duplicated
// so the two surfaces cannot drift; it belongs in core/utils, which is a move for whoever next has
// reason to touch auth.
import '../../../auth/presentation/security_activity_labels.dart';
import '../../domain/entities/post.dart';
import 'post_body_text.dart';
import 'post_media_view.dart';
import 'post_poll_view.dart';
import 'report_sheet.dart';

/// One post in a feed.
class PostCard extends StatelessWidget {
  const PostCard({
    super.key,
    required this.post,
    this.onLike,
    this.onSave,
    this.onVote,
    this.onDelete,
    this.onEdit,
    this.onShare,
    this.onTap,
  });

  final Post post;
  final VoidCallback? onLike;
  final VoidCallback? onSave;
  final void Function(String optionId)? onVote;
  final VoidCallback? onDelete;
  final VoidCallback? onEdit;
  final VoidCallback? onShare;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return KurxCard(
      onTap: onTap ?? () => context.push('/posts/${post.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _Header(post: post, onDelete: onDelete, onEdit: onEdit),
          if (post.body.isNotEmpty) ...[
            const SizedBox(height: KSpace.md),
            PostBodyText(post.body),
          ],
          if (post.media.isNotEmpty) ...[
            const SizedBox(height: KSpace.md),
            PostMediaView(media: post.media),
          ],
          if (post.poll != null) ...[
            const SizedBox(height: KSpace.md),
            PostPollView(poll: post.poll!, onVote: onVote),
          ],
          if (post.event != null) ...[
            const SizedBox(height: KSpace.md),
            _EventAttachment(event: post.event!),
          ],
          if (post.sharedPost != null) ...[
            const SizedBox(height: KSpace.md),
            _SharedPost(post: post.sharedPost!),
          ],
          const SizedBox(height: KSpace.md),
          Divider(height: 1, color: c.border),
          const SizedBox(height: KSpace.sm),
          Row(
            children: [
              _Action(
                icon: post.likedByMe ? Icons.favorite_rounded : Icons.favorite_border_rounded,
                label: post.likeCount > 0 ? '${post.likeCount}' : null,
                active: post.likedByMe,
                tooltip: post.likedByMe ? 'Unlike' : 'Like',
                onTap: onLike,
              ),
              _Action(
                icon: Icons.mode_comment_outlined,
                label: post.commentCount > 0 ? '${post.commentCount}' : null,
                tooltip: 'Comments',
                onTap: () => context.push('/posts/${post.id}'),
              ),
              _Action(
                icon: Icons.repeat_rounded,
                label: post.shareCount > 0 ? '${post.shareCount}' : null,
                tooltip: 'Share',
                onTap: onShare,
              ),
              const Spacer(),
              _Action(
                icon: post.savedByMe ? Icons.bookmark_rounded : Icons.bookmark_border_rounded,
                active: post.savedByMe,
                tooltip: post.savedByMe ? 'Unsave' : 'Save',
                onTap: onSave,
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.post, this.onDelete, this.onEdit});

  final Post post;
  final VoidCallback? onDelete;
  final VoidCallback? onEdit;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final handle = post.author.username;
    final visibility = PostVisibility.fromWire(post.visibility).label;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // The avatar opens the author's profile and announced as nothing at all. When there is no
        // handle there is no profile, so it is not a control either — the label and the button role
        // both follow the tap being real.
        Semantics(
          button: handle != null,
          label: handle == null ? null : "${post.author.name}'s profile",
          child: GestureDetector(
            onTap: handle == null ? null : () => context.push('/u/$handle'),
            child: KurxAvatar(name: post.author.name, imageUrl: post.author.avatarKey, size: 40),
          ),
        ),
        const SizedBox(width: KSpace.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Flexible(
                    child: Text(
                      post.author.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context)
                          .textTheme
                          .bodyMedium
                          ?.copyWith(fontWeight: FontWeight.w700, color: c.text),
                    ),
                  ),
                  if (post.author.isVerified) ...[
                    const SizedBox(width: KSpace.xs),
                    Icon(Icons.verified_rounded, size: 14, color: c.accent),
                  ],
                ],
              ),
              Text(
                [
                  if (handle != null) '@$handle',
                  relativeTime(post.createdAt),
                  if (post.editedAt != null) 'edited',
                  visibility,
                ].join(' · '),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
              ),
            ],
          ),
        ),
        IconButton(
          icon: Icon(Icons.more_horiz_rounded, color: c.muted),
          tooltip: 'Post options',
          onPressed: () => _openMenu(context),
        ),
      ],
    );
  }

  /// Author actions and reader actions live in one menu, filtered by what the server says this
  /// viewer may do (`can_edit`/`can_delete`) — never by comparing ids locally.
  Future<void> _openMenu(BuildContext context) async {
    final c = context.kurx;
    final choice = await showModalBottomSheet<String>(
      context: context,
      builder: (sheet) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (post.canEdit)
              ListTile(
                leading: const Icon(Icons.edit_outlined),
                title: const Text('Edit post'),
                onTap: () => Navigator.of(sheet).pop('edit'),
              ),
            if (post.canDelete)
              ListTile(
                leading: Icon(Icons.delete_outline_rounded, color: c.danger),
                title: Text('Delete post', style: TextStyle(color: c.danger)),
                onTap: () => Navigator.of(sheet).pop('delete'),
              ),
            // Reporting your own post is not offered — there is nobody to escalate to.
            if (!post.canDelete)
              ListTile(
                leading: const Icon(Icons.flag_outlined),
                title: const Text('Report post'),
                onTap: () => Navigator.of(sheet).pop('report'),
              ),
            ListTile(
              leading: const Icon(Icons.close_rounded),
              title: const Text('Cancel'),
              onTap: () => Navigator.of(sheet).pop(),
            ),
          ],
        ),
      ),
    );
    if (!context.mounted) return;

    switch (choice) {
      case 'edit':
        onEdit?.call();
      case 'delete':
        onDelete?.call();
      case 'report':
        final filed = await showReportSheet(context, entityType: 'post', entityId: post.id);
        if (filed && context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Reported. Our team will review it.')),
          );
        }
    }
  }
}

class _EventAttachment extends StatelessWidget {
  const _EventAttachment({required this.event});

  final PostEventRef event;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return InkWell(
      onTap: () => context.push('/events/${event.slug}'),
      borderRadius: BorderRadius.circular(KRadius.md),
      child: Container(
        decoration: BoxDecoration(
          border: Border.all(color: c.border),
          borderRadius: BorderRadius.circular(KRadius.md),
        ),
        clipBehavior: Clip.antiAlias,
        child: Row(
          children: [
            // `bannerUrl` is presigned; `bannerKey` beside it is a storage key and is NOT fetchable.
            // This passed the key, so every event referenced by a post rendered an empty box — the
            // errorBuilder swallowed the failure and made it look like a design with no image (D-302).
            if (event.bannerUrl != null && event.bannerUrl!.isNotEmpty)
              Image.network(
                event.bannerUrl!,
                width: 96,
                height: 72,
                fit: BoxFit.cover,
                errorBuilder: (_, _, _) => const SizedBox(width: 96, height: 72),
              ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(KSpace.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      event.title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context)
                          .textTheme
                          .bodyMedium
                          ?.copyWith(fontWeight: FontWeight.w600, color: c.text),
                    ),
                    const SizedBox(height: KSpace.xs),
                    Text(
                      [
                        '${event.startsAt.day}/${event.startsAt.month}/${event.startsAt.year}',
                        if (event.city != null) event.city!,
                      ].join(' · '),
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A shared post renders read-only and never recursively — the backend guarantees exactly one level
/// (`cannot_share_a_share`), so there is no nested share to handle here.
class _SharedPost extends StatelessWidget {
  const _SharedPost({required this.post});

  final Post post;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        border: Border.all(color: c.border),
        borderRadius: BorderRadius.circular(KRadius.md),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              KurxAvatar(name: post.author.name, imageUrl: post.author.avatarKey, size: 24),
              const SizedBox(width: KSpace.sm),
              Expanded(
                child: Text(
                  post.author.name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context)
                      .textTheme
                      .bodySmall
                      ?.copyWith(fontWeight: FontWeight.w600, color: c.text),
                ),
              ),
            ],
          ),
          if (post.body.isNotEmpty) ...[
            const SizedBox(height: KSpace.sm),
            PostBodyText(post.body, maxLines: 4, muted: true),
          ],
          if (post.media.isNotEmpty) ...[
            const SizedBox(height: KSpace.sm),
            PostMediaView(media: post.media, compact: true),
          ],
        ],
      ),
    );
  }
}

class _Action extends StatelessWidget {
  const _Action({required this.icon, required this.tooltip, this.label, this.active = false, this.onTap});

  final IconData icon;
  final String tooltip;
  final String? label;
  final bool active;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final color = active ? c.accent : c.muted;
    return Tooltip(
      message: tooltip,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(KRadius.pill),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: KSpace.sm),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 18, color: color),
              if (label != null) ...[
                const SizedBox(width: KSpace.xs),
                Text(label!, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: color)),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
