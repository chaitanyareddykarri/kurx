import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../auth/presentation/security_activity_labels.dart';
import '../../domain/entities/post.dart';
import '../providers/posts_providers.dart';
import '../widgets/post_body_text.dart';
import '../widgets/post_card.dart';

/// A single post and its comments, one level of reply deep. A reply to a reply attaches to the same
/// parent — the server enforces it, and flattening here keeps the thread readable rather than
/// stair-stepping off the right edge of a phone.
class PostDetailPage extends ConsumerStatefulWidget {
  const PostDetailPage({super.key, required this.postId});

  final String postId;

  @override
  ConsumerState<PostDetailPage> createState() => _PostDetailPageState();
}

class _PostDetailPageState extends ConsumerState<PostDetailPage> {
  final _comment = TextEditingController();
  List<PostComment>? _comments;
  PostComment? _replyTo;
  bool _sending = false;
  String? _error;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final body = _comment.text.trim();
    if (body.isEmpty || _sending) return;

    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      final created = await ref.read(postsSourceProvider).addComment(
            widget.postId,
            body,
            // Replying to a reply attaches to its parent, never to the reply itself.
            parentCommentId: _replyTo?.parentCommentId ?? _replyTo?.id,
          );
      setState(() {
        _comments = [...?_comments, created];
        _comment.clear();
        _replyTo = null;
        _sending = false;
      });
    } on ApiError catch (e) {
      setState(() {
        _sending = false;
        _error = e.code;
      });
    }
  }

  Future<void> _toggleCommentLike(PostComment comment) async {
    final result =
        await ref.read(postsSourceProvider).setCommentLike(comment.id, !comment.likedByMe);
    setState(() {
      _comments = [
        for (final c in _comments ?? const <PostComment>[])
          c.id == comment.id ? c.copyWith(likedByMe: result.liked, likeCount: result.likeCount) : c,
      ];
    });
  }

  Future<void> _deleteComment(PostComment comment) async {
    final snapshot = _comments;
    setState(() {
      _comments = [
        for (final c in _comments ?? const <PostComment>[])
          if (c.id != comment.id && c.parentCommentId != comment.id) c,
      ];
    });
    try {
      await ref.read(postsSourceProvider).deleteComment(comment.id);
    } on ApiError {
      setState(() => _comments = snapshot);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final post = ref.watch(postDetailProvider(widget.postId));
    final loaded = ref.watch(postCommentsProvider(widget.postId));

    // Seed once from the server, then own the list locally so an add/delete does not refetch.
    _comments ??= loaded.valueOrNull?.items;
    final comments = _comments ?? const <PostComment>[];
    final roots = comments.where((x) => !x.isReply).toList();

    return Scaffold(
      appBar: AppBar(title: const Text('Post')),
      body: AsyncValueView(
        value: post,
        onRetry: () => ref.invalidate(postDetailProvider(widget.postId)),
        data: (p) => ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            PostCard(
              post: p,
              // `onTap: () {}` used to sit here. `PostCard` treats a null `onTap` as "not tappable"
              // and otherwise defaults to opening the post — but this IS the post's own page, so an
              // empty callback made the card announce as a control that opens what is already open.
              onTap: null,
              onEdit: () async {
                final updated = await context.push<Post>('/posts/${p.id}/edit', extra: p);
                if (updated != null) ref.invalidate(postDetailProvider(widget.postId));
              },
            ),
            const SizedBox(height: KSpace.lg),
            Text(
              'Comments',
              style: Theme.of(context)
                  .textTheme
                  .titleSmall
                  ?.copyWith(fontWeight: FontWeight.w700, color: c.text),
            ),
            const SizedBox(height: KSpace.md),
            if (roots.isEmpty)
              Text('No comments yet.', style: TextStyle(color: c.muted))
            else
              for (final root in roots) ...[
                _CommentTile(
                  comment: root,
                  onLike: () => _toggleCommentLike(root),
                  onDelete: () => _deleteComment(root),
                  onReply: () => setState(() => _replyTo = root),
                ),
                for (final reply in comments.where((x) => x.parentCommentId == root.id))
                  Padding(
                    padding: const EdgeInsets.only(left: KSpace.xl),
                    child: _CommentTile(
                      comment: reply,
                      onLike: () => _toggleCommentLike(reply),
                      onDelete: () => _deleteComment(reply),
                      onReply: () => setState(() => _replyTo = reply),
                    ),
                  ),
              ],
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(KSpace.md),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (_replyTo != null)
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        'Replying to ${_replyTo!.author.name}',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
                      ),
                    ),
                    TextButton(
                      onPressed: () => setState(() => _replyTo = null),
                      child: const Text('Cancel'),
                    ),
                  ],
                ),
              if (_error != null)
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(_error!, style: TextStyle(color: c.danger)),
                ),
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _comment,
                      maxLength: PostLimits.commentMax,
                      onChanged: (_) => setState(() {}),
                      decoration: const InputDecoration(
                        hintText: 'Add a comment…',
                        counterText: '',
                        border: OutlineInputBorder(),
                      ),
                    ),
                  ),
                  IconButton(
                    icon: _sending
                        ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Icon(Icons.send_rounded),
                    tooltip: 'Post comment',
                    onPressed: _comment.text.trim().isEmpty || _sending ? null : _send,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CommentTile extends StatelessWidget {
  const _CommentTile({
    required this.comment,
    required this.onLike,
    required this.onDelete,
    required this.onReply,
  });

  final PostComment comment;
  final VoidCallback onLike;
  final VoidCallback onDelete;
  final VoidCallback onReply;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.md),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          KurxAvatar(name: comment.author.name, imageUrl: comment.author.avatarKey, size: 32),
          const SizedBox(width: KSpace.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(KSpace.md),
                  decoration: BoxDecoration(
                    color: c.elevated,
                    borderRadius: BorderRadius.circular(KRadius.md),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        comment.author.name,
                        style: Theme.of(context)
                            .textTheme
                            .bodySmall
                            ?.copyWith(fontWeight: FontWeight.w700, color: c.text),
                      ),
                      const SizedBox(height: KSpace.xs),
                      PostBodyText(comment.body),
                    ],
                  ),
                ),
                Row(
                  children: [
                    Text(
                      relativeTime(comment.createdAt),
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
                    ),
                    TextButton.icon(
                      onPressed: onLike,
                      icon: Icon(
                        comment.likedByMe ? Icons.favorite_rounded : Icons.favorite_border_rounded,
                        size: 14,
                        color: comment.likedByMe ? c.accent : c.muted,
                      ),
                      label: Text(comment.likeCount > 0 ? '${comment.likeCount}' : 'Like'),
                    ),
                    TextButton(onPressed: onReply, child: const Text('Reply')),
                    if (comment.canDelete)
                      IconButton(
                        icon: Icon(Icons.delete_outline_rounded, size: 16, color: c.muted),
                        tooltip: 'Delete comment',
                        onPressed: onDelete,
                      ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
