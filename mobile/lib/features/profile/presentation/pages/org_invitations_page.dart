import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../orders/data/models/attendee_dtos.dart';
import '../../../orders/presentation/providers/attendee_providers.dart';

/// Organisation invitations inbox — `GET /v1/me/org-invitations`.
///
/// ## Why there are no Accept / Decline buttons here
///
/// Accept and decline are keyed by **token** (`POST /v1/org-invitations/{token}/accept`), and the
/// list endpoint does not return one: `OrgInvitationService.ListMineAsync` selects
/// `Id, OrgId, OrgName, InvitedPhone, Role, Status, ExpiresAt, CreatedAt` and `OrgInvitationView`
/// has no `Token` field at all. The token only ever reaches the invitee in the invite message.
///
/// Rendering buttons here would mean inventing a token or calling with the row id, and the server
/// would answer `not_found` every time. So the inbox shows what is pending and points at the link;
/// [OrgInvitationDeepLinkPage] does the accepting when that link is opened.
///
/// **One-line backend fix:** add `Token` to `OrgInvitationView` + the `ListMineAsync` projection,
/// and this screen becomes fully actionable with no other change.
class OrgInvitationsPage extends ConsumerWidget {
  const OrgInvitationsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Organisation invites')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(myOrgInvitationsProvider);
          await ref.read(myOrgInvitationsProvider.future);
        },
        child: AsyncValueView(
          value: ref.watch(myOrgInvitationsProvider),
          onRetry: () => ref.invalidate(myOrgInvitationsProvider),
          isEmpty: (list) => list.isEmpty,
          empty: const EmptyState(
            icon: Icons.mail_outline_rounded,
            title: 'No pending invites',
            message: 'Invitations to represent an organisation will appear here.',
          ),
          data: (invites) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: invites.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.md),
            itemBuilder: (_, i) => _InviteCard(invite: invites[i]),
          ),
        ),
      ),
    );
  }
}

class _InviteCard extends StatelessWidget {
  const _InviteCard({required this.invite});
  final OrgInvitationDto invite;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final scheme = Theme.of(context).colorScheme;
    final expires = invite.expiresAt;
    final expired = expires != null && expires.isBefore(DateTime.now());

    return Container(
      padding: const EdgeInsets.all(KSpace.lg),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(invite.orgName ?? 'An organisation',
              style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 16)),
          const SizedBox(height: KSpace.xs),
          Text('Invited as ${invite.role}', style: TextStyle(color: c.muted)),
          if (expires != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(
              expired
                  ? 'Expired ${DateFormat('d MMM yyyy').format(expires.toLocal())}'
                  : 'Expires ${DateFormat('d MMM yyyy').format(expires.toLocal())}',
              style: TextStyle(color: expired ? scheme.error : c.muted, fontSize: 13),
            ),
          ],
          const SizedBox(height: KSpace.md),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(Icons.link_rounded, size: 16, color: c.muted),
              const SizedBox(width: KSpace.sm),
              Expanded(
                child: Text(
                  'Open the invite link sent to you to accept or decline.',
                  style: TextStyle(color: c.muted, fontSize: 13),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Landing screen for `kurx://org-invitations/{token}` — where accept and decline actually happen,
/// because the token is in the URL.
class OrgInvitationDeepLinkPage extends ConsumerStatefulWidget {
  const OrgInvitationDeepLinkPage({super.key, required this.token});
  final String token;

  @override
  ConsumerState<OrgInvitationDeepLinkPage> createState() => _OrgInvitationDeepLinkPageState();
}

class _OrgInvitationDeepLinkPageState extends ConsumerState<OrgInvitationDeepLinkPage> {
  bool _busy = false;
  String? _error;
  String? _done;

  Future<void> _run({required bool accept}) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final actions = ref.read(attendeeActionsProvider);
      if (accept) {
        await actions.acceptOrgInvitation(widget.token);
      } else {
        await actions.declineOrgInvitation(widget.token);
      }
      if (!mounted) return;
      setState(() {
        _busy = false;
        _done = accept ? 'You are now a member.' : 'Invitation declined.';
      });
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = switch (e.code) {
          'not_found' => 'This invitation link is not valid.',
          'not_pending' => 'This invitation has already been used.',
          'expired' => 'This invitation has expired. Ask for a new one.',
          'phone_mismatch' =>
            'This invitation was sent to a different mobile number than the one you are signed in with.',
          _ => e.userMessage,
        };
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final done = _done;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Organisation invite')),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(KSpace.xl),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                done != null ? Icons.check_circle_rounded : Icons.groups_rounded,
                size: 56,
                color: c.accent,
              ),
              const SizedBox(height: KSpace.lg),
              Text(
                done ?? 'Join this organisation?',
                textAlign: TextAlign.center,
                style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 18),
              ),
              if (done == null) ...[
                const SizedBox(height: KSpace.sm),
                Text(
                  'Accepting makes you a member and lets you act on its behalf.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: c.muted),
                ),
                if (_error != null) ...[
                  const SizedBox(height: KSpace.lg),
                  Semantics(
                    liveRegion: true,
                    child: Text(_error!,
                        textAlign: TextAlign.center,
                        style: TextStyle(color: Theme.of(context).colorScheme.error)),
                  ),
                ],
                const SizedBox(height: KSpace.xl),
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        onPressed: _busy ? null : () => _run(accept: false),
                        child: const Text('Decline'),
                      ),
                    ),
                    const SizedBox(width: KSpace.md),
                    Expanded(
                      child: FilledButton(
                        onPressed: _busy ? null : () => _run(accept: true),
                        child: _busy
                            ? const SizedBox(
                                height: 18,
                                width: 18,
                                child: CircularProgressIndicator(strokeWidth: 2))
                            : const Text('Accept'),
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
