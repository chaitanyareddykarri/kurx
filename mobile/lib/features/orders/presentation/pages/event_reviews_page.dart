import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/attendee_dtos.dart';
import '../providers/attendee_providers.dart';

/// Event reviews — `EventReviewEndpoints`. Reading is public; posting requires an issued ticket
/// (`review_requires_ticket`, 403). POST is an **upsert**, so "write" and "edit" are one action.
class EventReviewsPage extends ConsumerWidget {
  const EventReviewsPage({super.key, required this.eventId, required this.eventTitle});

  final String eventId;
  final String eventTitle;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Reviews'),
        actions: [
          TextButton.icon(
            onPressed: () => _writeReview(context, ref),
            icon: const Icon(Icons.rate_review_outlined, size: 18),
            label: const Text('Write'),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(eventReviewsProvider(eventId));
          await ref.read(eventReviewsProvider(eventId).future);
        },
        child: AsyncValueView(
          value: ref.watch(eventReviewsProvider(eventId)),
          onRetry: () => ref.invalidate(eventReviewsProvider(eventId)),
          isEmpty: (page) => page.items.isEmpty,
          empty: EmptyState(
            icon: Icons.reviews_outlined,
            title: 'No reviews yet',
            message: 'Been to $eventTitle? Share how it went.',
            actionLabel: 'Write a review',
            onAction: () => _writeReview(context, ref),
          ),
          data: (page) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: page.items.length + 1,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
            itemBuilder: (_, i) => i == 0
                ? _SummaryHeader(average: page.average, count: page.count)
                : _ReviewCard(review: page.items[i - 1]),
          ),
        ),
      ),
    );
  }

  Future<void> _writeReview(BuildContext context, WidgetRef ref) async {
    final posted = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: context.kurx.cardSurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(KRadius.xl)),
      ),
      builder: (_) => _WriteReviewSheet(eventId: eventId, eventTitle: eventTitle, ref: ref),
    );

    if (posted == true && context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Thanks — your review is live.')));
    }
  }
}

/// A real [StatefulWidget] rather than a `StatefulBuilder`: the sheet owns four pieces of state
/// (rating, anonymity, in-flight, error) that must survive a rebuild, and a builder-scoped `var`
/// silently resets them every time the sheet repaints.
class _WriteReviewSheet extends StatefulWidget {
  const _WriteReviewSheet({
    required this.eventId,
    required this.eventTitle,
    required this.ref,
  });

  final String eventId;
  final String eventTitle;
  final WidgetRef ref;

  @override
  State<_WriteReviewSheet> createState() => _WriteReviewSheetState();
}

class _WriteReviewSheetState extends State<_WriteReviewSheet> {
  final _title = TextEditingController();
  final _body = TextEditingController();
  int _rating = 5;
  bool _anonymous = false;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _post() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.ref.read(attendeeActionsProvider).upsertReview(
            widget.eventId,
            rating: _rating,
            title: _title.text.trim().isEmpty ? null : _title.text.trim(),
            body: _body.text.trim().isEmpty ? null : _body.text.trim(),
            isAnonymous: _anonymous,
          );
      if (mounted) Navigator.of(context).pop(true);
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = e.code == 'review_requires_ticket'
            ? 'Only people who held a ticket for this event can review it.'
            : e.userMessage;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: EdgeInsets.fromLTRB(
        KSpace.lg,
        KSpace.lg,
        KSpace.lg,
        MediaQuery.viewInsetsOf(context).bottom + KSpace.lg,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Rate ${widget.eventTitle}',
                style: TextStyle(color: c.text, fontWeight: FontWeight.w800, fontSize: 17)),
            const SizedBox(height: KSpace.lg),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                for (var star = 1; star <= 5; star++)
                  IconButton(
                    onPressed: _busy ? null : () => setState(() => _rating = star),
                    icon: Icon(
                      star <= _rating ? Icons.star_rounded : Icons.star_border_rounded,
                      size: 34,
                      color: c.accent,
                    ),
                    tooltip: '$star star${star == 1 ? '' : 's'}',
                  ),
              ],
            ),
            const SizedBox(height: KSpace.md),
            TextField(
              controller: _title,
              enabled: !_busy,
              textCapitalization: TextCapitalization.sentences,
              decoration: const InputDecoration(labelText: 'Headline (optional)'),
            ),
            const SizedBox(height: KSpace.md),
            TextField(
              controller: _body,
              enabled: !_busy,
              maxLines: 4,
              textCapitalization: TextCapitalization.sentences,
              decoration: const InputDecoration(labelText: 'What stood out?'),
            ),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Post anonymously'),
              value: _anonymous,
              onChanged: _busy ? null : (v) => setState(() => _anonymous = v),
            ),
            if (_error != null) ...[
              const SizedBox(height: KSpace.sm),
              Semantics(
                liveRegion: true,
                child:
                    Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ),
            ],
            const SizedBox(height: KSpace.lg),
            FilledButton(
              onPressed: _busy ? null : _post,
              child: _busy
                  ? const SizedBox(
                      height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Post review'),
            ),
          ],
        ),
      ),
    );
  }
}

class _SummaryHeader extends StatelessWidget {
  const _SummaryHeader({required this.average, required this.count});
  final double average;
  final int count;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Row(
        children: [
          Text(average.toStringAsFixed(1),
              style: TextStyle(color: c.text, fontWeight: FontWeight.w800, fontSize: 32)),
          const SizedBox(width: KSpace.lg),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    for (var star = 1; star <= 5; star++)
                      Icon(
                        star <= average.round()
                            ? Icons.star_rounded
                            : Icons.star_border_rounded,
                        size: 18,
                        color: c.accent,
                      ),
                  ],
                ),
                const SizedBox(height: KSpace.xs),
                Text('$count review${count == 1 ? '' : 's'}',
                    style: TextStyle(color: c.muted, fontSize: 13)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ReviewCard extends StatelessWidget {
  const _ReviewCard({required this.review});
  final ReviewDto review;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final created = review.createdAt;

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  review.isAnonymous ? 'Anonymous' : (review.authorName ?? 'Attendee'),
                  style: TextStyle(color: c.text, fontWeight: FontWeight.w600),
                ),
              ),
              if (review.isVerified) ...[
                Icon(Icons.verified_rounded, size: 15, color: c.accent),
                const SizedBox(width: KSpace.xs),
                Text('Attended', style: TextStyle(color: c.accent, fontSize: 12)),
              ],
            ],
          ),
          const SizedBox(height: KSpace.xs),
          Row(
            children: [
              for (var star = 1; star <= 5; star++)
                Icon(
                  star <= review.rating ? Icons.star_rounded : Icons.star_border_rounded,
                  size: 15,
                  color: c.accent,
                ),
              if (created != null) ...[
                const SizedBox(width: KSpace.sm),
                Text(DateFormat('d MMM yyyy').format(created.toLocal()),
                    style: TextStyle(color: c.muted, fontSize: 12)),
              ],
            ],
          ),
          if (review.title != null && review.title!.isNotEmpty) ...[
            const SizedBox(height: KSpace.sm),
            Text(review.title!,
                style: TextStyle(color: c.text, fontWeight: FontWeight.w700)),
          ],
          if (review.body != null && review.body!.isNotEmpty) ...[
            const SizedBox(height: KSpace.xs),
            Text(review.body!, style: TextStyle(color: c.text.withValues(alpha: 0.85))),
          ],
        ],
      ),
    );
  }
}
