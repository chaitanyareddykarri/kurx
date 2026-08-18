import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/org_dto.dart';
import '../providers/organizer_providers.dart';

/// **Representing** — representation details on the user's profile (D-267).
///
/// This is *information about the caller's authority*, not a place to navigate from. It replaced
/// *My Organizations*, which was the door into the org-first workflow: open the list → open an
/// organisation → open its events → create an event.
///
/// Deliberately **not** an organisation hub: no tile grid, no organisation dashboard, no organisation
/// analytics, no route from here to any event. Each row states which institution the person may act
/// for, what authority they hold, and whether it is verified — and offers **Verification**, the one
/// action that is genuinely about representation. Events live in Workspace.
///
/// There is no "Create organization" action, and there never should be: a person does not mint an
/// institution, they submit a representation *request* that an admin verifies (D-074/D-075). D-382
/// added the route to that request — `/representing/new` — which is that act, not organization
/// creation: what it stages is hidden and PendingReview until an admin approves it.
class RepresentingPage extends ConsumerWidget {
  const RepresentingPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Representing')),
      body: AsyncValueView(
        value: ref.watch(myRepresentationsProvider),
        onRetry: () => ref.invalidate(myRepresentationsProvider),
        isEmpty: (list) => list.isEmpty,
        // D-382 — the old empty state said "Personal events need none. You are asked who you represent
        // while creating an event", which stopped being true at D-379 (every event represents a real
        // organization) and pointed at a step that pointed back here. Now it says what to do and
        // offers the action.
        empty: Center(
          child: Padding(
            padding: const EdgeInsets.all(KSpace.lg),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const EmptyState(
                  icon: Icons.verified_user_outlined,
                  title: 'You represent no organisation',
                  message:
                      'Every event is hosted on behalf of an organisation Kurx has verified. Request '
                      'representation and an admin reviews it before it can back an event.',
                ),
                const SizedBox(height: KSpace.md),
                Builder(
                  builder: (context) => FilledButton(
                    onPressed: () => context.push('/representing/new'),
                    child: const Text('Represent an organisation'),
                  ),
                ),
              ],
            ),
          ),
        ),
        data: (orgs) => ListView.separated(
          padding: const EdgeInsets.all(KSpace.lg),
          // One extra row: the way to request another institution (D-382).
          itemCount: orgs.length + 1,
          separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
          itemBuilder: (context, i) => i == orgs.length
              ? Align(
                  alignment: Alignment.centerLeft,
                  child: TextButton(
                    onPressed: () => context.push('/representing/new'),
                    child: const Text('Represent another organisation'),
                  ),
                )
              : _RepresentationCard(representation: orgs[i]),
        ),
      ),
    );
  }
}

class _RepresentationCard extends StatelessWidget {
  const _RepresentationCard({required this.representation});
  final RepresentationDto representation;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.xl),
        boxShadow: kCardShadow(context),
      ),
      child: Padding(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              representation.name,
              style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15),
            ),
            const SizedBox(height: KSpace.sm),
            // Verification status is not on this list shape — only the org detail endpoint carries it,
            // and showing a status every row would wear regardless of the truth is worse than showing
            // none (D-064). The Verification action below reads the real one.
            _Chip(label: representation.authority, color: c.accent),
            const SizedBox(height: KSpace.sm),
            Text(
              'You may host events representing this organisation. Choose it on the Representing '
              'step when you create one.',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.muted),
            ),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton(
                onPressed: () =>
                    context.push('/representing/${representation.organizationId}/verification'),
                child: const Text('Verification'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.label, required this.color});
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(KRadius.pill),
      ),
      child: Text(
        label,
        style: TextStyle(color: color, fontSize: 11, fontWeight: FontWeight.w700),
      ),
    );
  }
}
