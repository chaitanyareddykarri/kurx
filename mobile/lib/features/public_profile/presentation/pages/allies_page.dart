import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../common/widgets/kurx_card.dart';
import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/public_profile_dto.dart';
import '../providers/public_profile_providers.dart';
import '../widgets/mutual_detail_sheet.dart';
import '../../../../common/widgets/kurx_shell_app_bar.dart';

/// "My Allies" management screen: incoming requests, sent requests, current allies, and suggestions
/// ranked from real shared history (D-201/D-20x).
class AlliesPage extends ConsumerWidget {
  const AlliesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: KurxShellAppBar(
        title: 'Community',
        actions: [
          IconButton(
            icon: const Icon(Icons.person_search_outlined),
            tooltip: 'Find people',
            onPressed: () => context.push('/people-search'),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.symmetric(vertical: KSpace.lg),
        children: [
          ContentWidth(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
              child: _RequestsSection(),
            ),
          ),
          ContentWidth(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
              child: _SentSection(),
            ),
          ),
          ContentWidth(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
              child: _MineSection(),
            ),
          ),
          ContentWidth(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
              child: _SuggestionsSection(),
            ),
          ),
        ],
      ),
    );
  }
}

/// Compact loading indicator for an optional, self-hiding section (Requests/Sent/Suggestions) —
/// these don't get a full-page `AsyncValueView` because they're allowed to render as nothing when
/// the data turns out to be empty; they should not, however, render as nothing while loading or on
/// a failed fetch, which is what a bare `.when()` was doing before (D-211 audit finding).
class _SectionLoading extends StatelessWidget {
  const _SectionLoading();
  @override
  Widget build(BuildContext context) => const Padding(
        padding: EdgeInsets.symmetric(vertical: KSpace.md),
        child: Center(
          child: SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)),
        ),
      );
}

class _SectionError extends StatelessWidget {
  const _SectionError({required this.onRetry});
  final VoidCallback onRetry;
  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: KSpace.sm),
      child: Row(
        children: [
          Expanded(
            child: Text("Couldn't load this — check your connection.",
                style: TextStyle(color: c.muted, fontSize: 13)),
          ),
          TextButton(onPressed: onRetry, child: const Text('Retry')),
        ],
      ),
    );
  }
}

class _SuggestionsSection extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(allySuggestionsProvider);
    return async.when(
      loading: () => const _SectionLoading(),
      error: (_, _) => _SectionError(onRetry: () => ref.invalidate(allySuggestionsProvider)),
      data: (suggestions) {
        if (suggestions.isEmpty) return const SizedBox.shrink();
        return _SectionBlock(
          title: 'Suggested Allies',
          child: Column(
            children: suggestions
                .map((s) => Padding(
                      padding: const EdgeInsets.only(bottom: KSpace.sm),
                      child: KurxCard(
                        onTap: s.username != null ? () => context.push('/u/${s.username}') : null,
                        child: Row(
                          children: [
                            KurxAvatar(name: s.name, imageUrl: s.avatarKey, size: 40),
                            const SizedBox(width: KSpace.md),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(s.name, style: TextStyle(color: context.kurx.text, fontWeight: FontWeight.w700)),
                                  Text(s.reason, style: TextStyle(color: context.kurx.muted, fontSize: 12)),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ))
                .toList(),
          ),
        );
      },
    );
  }
}

class _RequestsSection extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(incomingAllyRequestsProvider);
    return async.when(
      loading: () => const _SectionLoading(),
      error: (_, _) => _SectionError(onRetry: () => ref.invalidate(incomingAllyRequestsProvider)),
      data: (incoming) {
        if (incoming.isEmpty) return const SizedBox.shrink();
        return _SectionBlock(
          title: 'Requests',
          child: Column(
            children: incoming
                .map((c) => _ConnectionTile(
                      connection: c,
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          KurxButton(
                            label: 'Accept',
                            onPressed: () async {
                              await ref.read(allySourceProvider).accept(c.id);
                              ref.invalidate(incomingAllyRequestsProvider);
                              ref.invalidate(myAlliesProvider);
                            },
                          ),
                          const SizedBox(width: KSpace.sm),
                          KurxButton(
                            label: 'Decline',
                            variant: KurxButtonVariant.secondary,
                            onPressed: () async {
                              await ref.read(allySourceProvider).decline(c.id);
                              ref.invalidate(incomingAllyRequestsProvider);
                            },
                          ),
                        ],
                      ),
                    ))
                .toList(),
          ),
        );
      },
    );
  }
}

class _SentSection extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(outgoingAllyRequestsProvider);
    return async.when(
      loading: () => const _SectionLoading(),
      error: (_, _) => _SectionError(onRetry: () => ref.invalidate(outgoingAllyRequestsProvider)),
      data: (outgoing) {
        if (outgoing.isEmpty) return const SizedBox.shrink();
        return _SectionBlock(
          title: 'Sent',
          child: Column(
            children: outgoing
                .map((c) => _ConnectionTile(
                      connection: c,
                      trailing: KurxButton(
                        label: 'Cancel',
                        variant: KurxButtonVariant.secondary,
                        onPressed: () async {
                          await ref.read(allySourceProvider).revoke(c.id);
                          ref.invalidate(outgoingAllyRequestsProvider);
                        },
                      ),
                    ))
                .toList(),
          ),
        );
      },
    );
  }
}

class _MineSection extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(myAlliesProvider);
    return _SectionBlock(
      title: 'Allies',
      child: AsyncValueView<List<AllyConnectionDto>>(
        value: async,
        onRetry: () => ref.invalidate(myAlliesProvider),
        isEmpty: (d) => d.isEmpty,
        empty: const EmptyState(
          icon: Icons.handshake_outlined,
          title: 'No allies yet',
          message: "Connect with people you've worked with on Kurx.",
        ),
        data: (mine) => Column(
          children: mine
              .map((c) => _ConnectionTile(
                    connection: c,
                    trailing: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        // Per-connection visibility (D-219). The backend and web already supported
                        // this; Flutter could read `visibility` but never set it.
                        _VisibilityToggle(connection: c),
                        TextButton(
                          onPressed: () => showMutualDetailSheet(context, c.otherUserId, c.otherName),
                          child: const Text('Shared history'),
                        ),
                        const SizedBox(width: KSpace.sm),
                        KurxButton(
                          label: 'Remove',
                          variant: KurxButtonVariant.secondary,
                          onPressed: () async {
                            await ref.read(allySourceProvider).revoke(c.id);
                            ref.invalidate(myAlliesProvider);
                          },
                        ),
                      ],
                    ),
                  ))
              .toList(),
        ),
      ),
    );
  }
}

/// Toggles one ally's public visibility (D-219). Stateful only to hold the in-flight flag, so a slow
/// network can't be double-submitted and the user gets a spinner rather than a dead-looking button.
class _VisibilityToggle extends ConsumerStatefulWidget {
  const _VisibilityToggle({required this.connection});
  final AllyConnectionDto connection;

  @override
  ConsumerState<_VisibilityToggle> createState() => _VisibilityToggleState();
}

class _VisibilityToggleState extends ConsumerState<_VisibilityToggle> {
  bool _busy = false;

  @override
  Widget build(BuildContext context) {
    final hidden = widget.connection.visibility.toLowerCase() == 'hidden';
    if (_busy) {
      return const Padding(
        padding: EdgeInsets.symmetric(horizontal: KSpace.sm),
        child: SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)),
      );
    }
    return IconButton(
      icon: Icon(hidden ? Icons.visibility_off_outlined : Icons.visibility_outlined),
      tooltip: hidden ? 'Hidden from your public profile' : 'Shown on your public profile',
      onPressed: () async {
        setState(() => _busy = true);
        try {
          await ref
              .read(allySourceProvider)
              .setVisibility(widget.connection.id, hidden ? 'public' : 'hidden');
          ref.invalidate(myAlliesProvider);
          if (context.mounted) {
            KurxFeedback.success(context, hidden ? 'Shown on your profile' : 'Hidden from your profile');
          }
        } catch (_) {
          // Left unchanged on failure — the list is not invalidated, so the icon still reflects the
          // server's last known state rather than an optimistic value that never persisted.
          if (context.mounted) KurxFeedback.error(context, "Couldn't update visibility");
        } finally {
          if (mounted) setState(() => _busy = false);
        }
      },
    );
  }
}

class _SectionBlock extends StatelessWidget {
  const _SectionBlock({required this.title, required this.child});
  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: TextStyle(color: c.text, fontSize: 16, fontWeight: FontWeight.w800)),
          const SizedBox(height: KSpace.md),
          child,
        ],
      ),
    );
  }
}

class _ConnectionTile extends StatelessWidget {
  const _ConnectionTile({required this.connection, required this.trailing});
  final AllyConnectionDto connection;
  final Widget trailing;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.sm),
      child: KurxCard(
        onTap: connection.otherUsername != null
            ? () => context.push('/u/${connection.otherUsername}')
            : null,
        child: Row(
          children: [
            KurxAvatar(name: connection.otherName, size: 40),
            const SizedBox(width: KSpace.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(connection.otherName, style: TextStyle(color: c.text, fontWeight: FontWeight.w700)),
                  if (connection.otherUsername != null)
                    Text('@${connection.otherUsername}', style: TextStyle(color: c.muted, fontSize: 12)),
                ],
              ),
            ),
            trailing,
          ],
        ),
      ),
    );
  }
}
