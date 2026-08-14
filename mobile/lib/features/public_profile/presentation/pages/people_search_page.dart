import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_user_tile.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../providers/public_profile_providers.dart';
import '../widgets/ally_connect_button.dart';

/// People search (D-20x) — the foundation for finding anyone with a public profile, not just
/// people you've already shared an event or org with.
class PeopleSearchPage extends ConsumerStatefulWidget {
  const PeopleSearchPage({super.key});

  @override
  ConsumerState<PeopleSearchPage> createState() => _PeopleSearchPageState();
}

class _PeopleSearchPageState extends ConsumerState<PeopleSearchPage> {
  final _controller = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Find people')),
      body: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: _controller,
              decoration: const InputDecoration(
                hintText: 'Search by name or @username',
                prefixIcon: Icon(Icons.search_rounded),
              ),
              onSubmitted: (v) => setState(() => _query = v.trim()),
            ),
            const SizedBox(height: KSpace.lg),
            if (_query.length < 2)
              const Expanded(
                child: EmptyState(
                  icon: Icons.person_search_outlined,
                  title: 'Search for someone',
                  message: 'Enter at least 2 characters, then press search.',
                ),
              )
            else
              Expanded(child: _Results(query: _query)),
          ],
        ),
      ),
    );
  }
}

class _Results extends ConsumerWidget {
  const _Results({required this.query});
  final String query;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final resultsAsync = ref.watch(peopleSearchProvider(query));
    final myId = ref.watch(currentUserProvider)?.id;

    return resultsAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (_, _) => const EmptyState(icon: Icons.error_outline, title: "Couldn't search"),
      data: (results) {
        if (results.isEmpty) {
          return EmptyState(icon: Icons.person_off_outlined, title: 'No public profiles match "$query"');
        }
        final otherIds = results.map((r) => r.id).where((id) => id != myId).toSet().toList();
        final statusAsync = ref.watch(allyStatusBatchProvider(otherIds));
        final status = statusAsync.valueOrNull ?? const {};

        return ListView.separated(
          itemCount: results.length,
          separatorBuilder: (_, _) => const SizedBox(height: KSpace.sm),
          itemBuilder: (_, i) {
            final r = results[i];
            return KurxUserTile(
              name: r.name,
              username: r.username,
              avatarKey: r.avatarKey,
              subtitle: r.headline,
              trailing: r.id == myId
                  ? null
                  : AllyConnectButton(targetUserId: r.id, initialRelation: _toRelation(status[r.id])),
            );
          },
        );
      },
    );
  }

  AllyRelation _toRelation(String? status) => switch (status) {
        'accepted' => AllyRelation.accepted,
        'pending_outgoing' => AllyRelation.outgoing,
        'pending_incoming' => AllyRelation.incoming,
        _ => AllyRelation.none,
      };
}
