import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/phone_field.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_manage_dto.dart';
import '../providers/organizer_providers.dart';
import '../widgets/organizer_form_sheet.dart';

class InvitationsPage extends ConsumerStatefulWidget {
  const InvitationsPage(
      {super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  ConsumerState<InvitationsPage> createState() => _InvitationsPageState();
}

class _InvitationsPageState extends ConsumerState<InvitationsPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 4, vsync: this);
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final param = (orgId: widget.orgId, eventId: widget.eventId);
    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Invitations'),
        actions: [
          IconButton(
            onPressed: () => _sendAll(context, ref),
            icon: const Icon(Icons.outgoing_mail, size: 20),
            tooltip: 'Send all pending',
          ),
          TextButton.icon(
            onPressed: () => _showInviteSheet(context, ref),
            icon: const Icon(Icons.person_add_outlined, size: 18),
            label: const Text('Invite'),
          ),
        ],
        bottom: TabBar(
          controller: _tabs,
          indicatorColor: c.accent,
          labelColor: c.accent,
          unselectedLabelColor: c.muted,
          isScrollable: true,
          tabs: const [
            Tab(text: 'All'),
            Tab(text: 'Pending'),
            Tab(text: 'Accepted'),
            Tab(text: 'Declined'),
          ],
        ),
      ),
      body: AsyncValueView(
        value: ref.watch(invitationsProvider(param)),
        onRetry: () => ref.invalidate(invitationsProvider(param)),
        data: (invitations) {
          final all = invitations;
          final pending =
              invitations.where((i) => i.status == 'pending').toList();
          final accepted =
              invitations.where((i) => i.status == 'accepted').toList();
          final declined =
              invitations.where((i) => i.status == 'declined').toList();

          return TabBarView(
            controller: _tabs,
            children: [
              _InvList(items: all, eventId: widget.eventId, orgId: widget.orgId),
              _InvList(items: pending, eventId: widget.eventId, orgId: widget.orgId),
              _InvList(items: accepted, eventId: widget.eventId, orgId: widget.orgId),
              _InvList(items: declined, eventId: widget.eventId, orgId: widget.orgId),
            ],
          );
        },
      ),
    );
  }

  Future<void> _showInviteSheet(BuildContext context, WidgetRef ref) async {
    final name = TextEditingController();
    final contact = TextEditingController();
    var phoneE164 = '';
    var channel = 'email';

    await OrganizerFormSheet.show(
      context,
      title: 'Invite a guest',
      submitLabel: 'Add invitation',
      successMessage: 'Guest added to the invite list.',
      fields: (enabled) => [
        TextField(
          controller: name,
          enabled: enabled,
          textCapitalization: TextCapitalization.words,
          decoration: const InputDecoration(labelText: 'Name'),
        ),
        const SizedBox(height: KSpace.md),
        StatefulBuilder(
          builder: (_, setInner) => Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              DropdownButtonFormField<String>(
                initialValue: channel,
                decoration: const InputDecoration(labelText: 'Send via'),
                items: const [
                  DropdownMenuItem(value: 'email', child: Text('Email')),
                  DropdownMenuItem(value: 'whatsapp', child: Text('WhatsApp')),
                ],
                onChanged: enabled ? (v) => setInner(() => channel = v ?? channel) : null,
              ),
              const SizedBox(height: KSpace.md),
              // The phone half gets the country picker rather than sharing the email text box: its
              // value reaches InvitationService.NormalizePhone, which reads digits with no '+' in the
              // legacy region, so an overseas guest entered in their own national format was invited —
              // and WhatsApped — at an unrelated Indian number.
              if (channel == 'email')
                TextField(
                  controller: contact,
                  enabled: enabled,
                  keyboardType: TextInputType.emailAddress,
                  decoration: const InputDecoration(labelText: 'Email address'),
                )
              else
                PhoneField(
                  label: 'Mobile number',
                  enabled: enabled,
                  onChanged: (e164, _) => phoneE164 = e164,
                ),
            ],
          ),
        ),
      ],
      // Adds to the list; sending is a separate, rate-limited bulk action so a typo does not
      // immediately fan out to a guest.
      onSubmit: () async {
        await ref.read(eventManageSourceProvider).addInvitation(
              widget.eventId,
              channel: channel,
              name: name.text.trim().isEmpty ? null : name.text.trim(),
              email: channel == 'email' ? contact.text.trim() : null,
              phone: channel == 'email' ? null : phoneE164,
            );
        ref.invalidate(invitationsProvider((orgId: widget.orgId, eventId: widget.eventId)));
      },
    );
  }

  Future<void> _sendAll(BuildContext context, WidgetRef ref) => confirmAndRun(
        context,
        title: 'Send all invitations',
        message: 'Every unsent invitation on this event will be delivered now. '
            'This cannot be undone.',
        confirmLabel: 'Send all',
        successMessage: 'Invitations sent.',
        action: () async {
          await ref.read(eventManageSourceProvider).sendInvitations(widget.eventId);
          ref.invalidate(invitationsProvider((orgId: widget.orgId, eventId: widget.eventId)));
        },
      );
}

class _InvList extends StatelessWidget {
  const _InvList({required this.items, required this.eventId, required this.orgId});
  final String eventId;
  final String orgId;
  final List<InvitationDto> items;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return const EmptyState(
        icon: Icons.mail_outline_rounded,
        title: 'No invitations here',
        message: 'Invitations will appear in this tab.',
      );
    }
    final c = context.kurx;
    return ListView.separated(
      padding: const EdgeInsets.all(KSpace.lg),
      itemCount: items.length,
      separatorBuilder: (_, _) => Divider(color: c.border),
      itemBuilder: (_, i) => _InvRow(eventId: eventId, orgId: orgId, inv: items[i]),
    );
  }
}

class _InvRow extends ConsumerWidget {
  const _InvRow({required this.inv, required this.eventId, required this.orgId});
  final InvitationDto inv;
  final String eventId;
  final String orgId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final (statusLabel, statusColor) = switch (inv.status) {
      'accepted' => ('Accepted', c.success),
      'declined' => ('Declined', c.danger),
      _ => ('Pending', c.warning),
    };

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading:
          KurxAvatar(name: inv.name ?? inv.email ?? inv.phone ?? '?', size: 40),
      title: Text(
        inv.name ?? inv.email ?? inv.phone ?? '',
        style: TextStyle(
            color: c.text, fontWeight: FontWeight.w600, fontSize: 14),
      ),
      subtitle: Text(
        DateFormat('dd MMM yyyy').format(inv.sentAt),
        style: TextStyle(color: c.muted, fontSize: 12),
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(
                horizontal: KSpace.sm, vertical: 2),
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(KRadius.pill),
            ),
            child: Text(statusLabel,
                style: TextStyle(
                    color: statusColor,
                    fontSize: 11,
                    fontWeight: FontWeight.w700)),
          ),
          // Resend and revoke both exist server-side (`POST /v1/invitations/{id}/resend`,
          // `DELETE /v1/invitations/{id}`); this menu used to be an empty onPressed.
          PopupMenuButton<String>(
            icon: Icon(Icons.more_vert_rounded, color: c.muted, size: 18),
            onSelected: (choice) => _act(context, ref, choice),
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'resend', child: Text('Resend')),
              PopupMenuItem(value: 'revoke', child: Text('Revoke')),
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _act(BuildContext context, WidgetRef ref, String choice) => confirmAndRun(
        context,
        title: choice == 'resend' ? 'Resend invitation' : 'Revoke invitation',
        message: choice == 'resend'
            ? 'Send this invitation again?'
            : 'Revoke this invitation? The guest will no longer be able to RSVP.',
        confirmLabel: choice == 'resend' ? 'Resend' : 'Revoke',
        successMessage: choice == 'resend' ? 'Invitation resent.' : 'Invitation revoked.',
        action: () async {
          final api = ref.read(eventManageSourceProvider);
          if (choice == 'resend') {
            await api.resendInvitation(inv.id);
          } else {
            await api.revokeInvitation(inv.id);
          }
          ref.invalidate(invitationsProvider((orgId: orgId, eventId: eventId)));
        },
      );
}
