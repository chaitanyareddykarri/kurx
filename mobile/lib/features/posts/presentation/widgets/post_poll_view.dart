import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/post.dart';

/// A poll on a post. Results stay hidden until the viewer votes or the poll closes — see
/// [PostPoll.showResults] for why that rule lives on the entity rather than here.
class PostPollView extends StatelessWidget {
  const PostPollView({super.key, required this.poll, this.onVote});

  final PostPoll poll;
  final void Function(String optionId)? onVote;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final canVote = poll.canVote && onVote != null;

    return Container(
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        border: Border.all(color: c.border),
        borderRadius: BorderRadius.circular(KRadius.md),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            poll.question,
            style: Theme.of(context)
                .textTheme
                .bodyMedium
                ?.copyWith(fontWeight: FontWeight.w600, color: c.text),
          ),
          if (poll.allowMultiple)
            Padding(
              padding: const EdgeInsets.only(top: KSpace.xs),
              child: Text(
                'Select as many as you like',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
              ),
            ),
          const SizedBox(height: KSpace.md),
          for (final o in poll.options) ...[
            _Option(
              option: o,
              poll: poll,
              enabled: canVote,
              onTap: canVote ? () => onVote!(o.id) : null,
            ),
            const SizedBox(height: KSpace.sm),
          ],
          Text(
            [
              '${poll.totalVotes} ${poll.totalVotes == 1 ? 'vote' : 'votes'}',
              if (poll.isClosed) 'closed',
            ].join(' · '),
            style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
          ),
        ],
      ),
    );
  }
}

class _Option extends StatelessWidget {
  const _Option({required this.option, required this.poll, required this.enabled, this.onTap});

  final PostPollOption option;
  final PostPoll poll;
  final bool enabled;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final pct = poll.totalVotes > 0 ? option.voteCount / poll.totalVotes : 0.0;

    return Semantics(
      selected: option.votedByMe,
      button: enabled,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(KRadius.sm),
        child: Container(
          decoration: BoxDecoration(
            border: Border.all(color: option.votedByMe ? c.accent : c.border),
            borderRadius: BorderRadius.circular(KRadius.sm),
          ),
          clipBehavior: Clip.antiAlias,
          child: Stack(
            children: [
              if (poll.showResults)
                Positioned.fill(
                  child: FractionallySizedBox(
                    alignment: Alignment.centerLeft,
                    widthFactor: pct,
                    child: ColoredBox(color: c.accent.withValues(alpha: 0.15)),
                  ),
                ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.sm + 2),
                child: Row(
                  children: [
                    if (option.votedByMe) ...[
                      Icon(Icons.check_rounded, size: 14, color: c.accent),
                      const SizedBox(width: KSpace.xs),
                    ],
                    Expanded(
                      child: Text(
                        option.text,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: option.votedByMe ? c.text : c.muted,
                            ),
                      ),
                    ),
                    if (poll.showResults)
                      Text(
                        '${(pct * 100).round()}%',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
