import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/public_profile_providers.dart';

enum AllyRelation { none, outgoing, incoming, accepted }

/// Request/accept/decline/revoke for one target user (D-201). Mirrors the web
/// `AllyConnectButton` state machine one-for-one.
class AllyConnectButton extends ConsumerStatefulWidget {
  const AllyConnectButton({
    super.key,
    required this.targetUserId,
    required this.initialRelation,
    this.initialConnectionId,
  });

  final String targetUserId;
  final AllyRelation initialRelation;
  final String? initialConnectionId;

  @override
  ConsumerState<AllyConnectButton> createState() => _AllyConnectButtonState();
}

class _AllyConnectButtonState extends ConsumerState<AllyConnectButton> {
  late AllyRelation _relation;
  String? _connectionId;
  bool _pending = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _relation = widget.initialRelation;
    _connectionId = widget.initialConnectionId;
  }

  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _pending = true;
      _error = null;
    });
    try {
      await action();
    } on ApiError catch (e) {
      setState(() => _error = e.userMessage);
    } finally {
      if (mounted) setState(() => _pending = false);
    }
  }

  Future<void> _connect() => _run(() async {
        final conn = await ref.read(allySourceProvider).request(widget.targetUserId);
        _connectionId = conn.id;
        _relation = conn.status == 'Accepted' ? AllyRelation.accepted : AllyRelation.outgoing;
      });

  Future<void> _respond(bool accept) => _run(() async {
        final id = _connectionId;
        if (id == null) return;
        if (accept) {
          await ref.read(allySourceProvider).accept(id);
          _relation = AllyRelation.accepted;
        } else {
          await ref.read(allySourceProvider).decline(id);
          _relation = AllyRelation.none;
        }
      });

  Future<void> _remove() => _run(() async {
        final id = _connectionId;
        if (id == null) return;
        await ref.read(allySourceProvider).revoke(id);
        _relation = AllyRelation.none;
        _connectionId = null;
      });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        switch (_relation) {
          AllyRelation.none => KurxButton(
              label: _pending ? 'Sending…' : 'Connect',
              onPressed: _pending ? null : _connect,
            ),
          AllyRelation.outgoing => KurxButton(
              label: _pending ? 'Cancelling…' : 'Requested',
              variant: KurxButtonVariant.secondary,
              onPressed: _pending ? null : _remove,
            ),
          AllyRelation.incoming => Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                KurxButton(label: 'Accept', onPressed: _pending ? null : () => _respond(true)),
                const SizedBox(width: 8),
                KurxButton(
                  label: 'Decline',
                  variant: KurxButtonVariant.secondary,
                  onPressed: _pending ? null : () => _respond(false),
                ),
              ],
            ),
          AllyRelation.accepted => KurxButton(
              label: _pending ? 'Removing…' : 'Ally ✓',
              variant: KurxButtonVariant.secondary,
              onPressed: _pending ? null : _remove,
            ),
        },
        if (_error != null)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(_error!, style: TextStyle(color: context.kurx.danger, fontSize: 11)),
          ),
      ],
    );
  }
}
