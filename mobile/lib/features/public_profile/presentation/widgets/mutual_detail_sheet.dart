import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/theme/design_tokens.dart';
import '../providers/public_profile_providers.dart';

/// "Shared history" bottom sheet (mobile pattern, not a desktop dialog) — shows shared events/orgs
/// on demand rather than prefetching for every row in a list.
Future<void> showMutualDetailSheet(BuildContext context, String otherUserId, String otherName) {
  return showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    builder: (_) => _MutualDetailSheet(otherUserId: otherUserId, otherName: otherName),
  );
}

class _MutualDetailSheet extends ConsumerWidget {
  const _MutualDetailSheet({required this.otherUserId, required this.otherName});
  final String otherUserId;
  final String otherName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final detailAsync = ref.watch(allyMutualDetailProvider(otherUserId));

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('You and $otherName', style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w800)),
            const SizedBox(height: KSpace.lg),
            detailAsync.when(
              loading: () => const Padding(
                padding: EdgeInsets.symmetric(vertical: KSpace.xl),
                child: Center(child: CircularProgressIndicator()),
              ),
              error: (_, _) => Text("Couldn't load shared history.", style: TextStyle(color: c.muted)),
              data: (detail) {
                if (detail.sharedEvents.isEmpty && detail.sharedOrgs.isEmpty) {
                  return Text('No shared public events or organizations found.', style: TextStyle(color: c.muted));
                }
                return ConstrainedBox(
                  constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * 0.5),
                  child: ListView(
                    shrinkWrap: true,
                    children: [
                      if (detail.sharedEvents.isNotEmpty) ...[
                        Text('Shared events', style: TextStyle(color: c.text, fontWeight: FontWeight.w700)),
                        const SizedBox(height: KSpace.sm),
                        ...detail.sharedEvents.map((e) => ListTile(
                              contentPadding: EdgeInsets.zero,
                              title: Text(e.title, style: TextStyle(color: c.accent)),
                              subtitle: Text(
                                '${e.startsAt.year}-${e.startsAt.month.toString().padLeft(2, '0')}',
                                style: TextStyle(color: c.muted, fontSize: 12),
                              ),
                              onTap: () {
                                Navigator.of(context).pop();
                                context.push('/events/${e.slug}');
                              },
                            )),
                      ],
                      if (detail.sharedOrgs.isNotEmpty) ...[
                        const SizedBox(height: KSpace.md),
                        Text('Shared organizations', style: TextStyle(color: c.text, fontWeight: FontWeight.w700)),
                        const SizedBox(height: KSpace.sm),
                        ...detail.sharedOrgs.map((o) => ListTile(
                              contentPadding: EdgeInsets.zero,
                              title: Text(o.name, style: TextStyle(color: c.accent)),
                            )),
                      ],
                    ],
                  ),
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}
