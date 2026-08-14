import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/content_width.dart';
import '../providers/account_providers.dart';

/// Blocked accounts (D-263). Unblocking is the only action here — blocking happens where you meet the
/// person (a post, a profile, a conversation), not from a list of people you have never met.
class BlockedUsersPage extends ConsumerWidget {
  const BlockedUsersPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(blockedUsersProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Blocked accounts')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Padding(padding: const EdgeInsets.all(24), child: Text('$e'))),
        data: (blocked) => ContentWidth(
          child: ListView(
            children: [
              const Padding(
                padding: EdgeInsets.all(16),
                child: Text(
                  'Blocking hides you from each other completely — posts, comments and messages, in both '
                  'directions. Neither of you can start a conversation with the other.',
                ),
              ),
              if (blocked.isEmpty)
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16),
                  child: Text("You haven't blocked anyone."),
                )
              else
                for (final u in blocked)
                  ListTile(
                    leading: CircleAvatar(
                      child: Text(u.name.isEmpty ? '?' : u.name.characters.first.toUpperCase()),
                    ),
                    title: Text(u.name),
                    subtitle: u.username == null ? null : Text('@${u.username}'),
                    trailing: TextButton(
                      onPressed: () async {
                        try {
                          await ref.read(accountDataSourceProvider).unblock(u.userId);
                          ref.invalidate(blockedUsersProvider);
                        } catch (e) {
                          if (!context.mounted) return;
                          ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
                        }
                      },
                      child: const Text('Unblock'),
                    ),
                  ),
            ],
          ),
        ),
      ),
    );
  }
}
