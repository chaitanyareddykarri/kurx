import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/content_width.dart';
import '../providers/account_providers.dart';

/// Account deletion (D-263).
///
/// Scheduled, not immediate: 30 days in which signing in and pressing cancel undoes it entirely. The
/// typed confirmation is a speed bump against a mis-tap — the server's step-up gate is the real
/// control, and a one-tap destructive action here is a support incident.
class DeleteAccountPage extends ConsumerStatefulWidget {
  const DeleteAccountPage({super.key});

  @override
  ConsumerState<DeleteAccountPage> createState() => _DeleteAccountPageState();
}

class _DeleteAccountPageState extends ConsumerState<DeleteAccountPage> {
  final _confirm = TextEditingController();
  final _reason = TextEditingController();
  bool _busy = false;

  @override
  void dispose() {
    _confirm.dispose();
    _reason.dispose();
    super.dispose();
  }

  Future<void> _request() async {
    if (_confirm.text.trim().toUpperCase() != 'DELETE') {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Type DELETE to confirm.')),
      );
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(accountDataSourceProvider).requestDeletion(
            reason: _reason.text.trim().isEmpty ? null : _reason.text.trim(),
          );
      ref.invalidate(accountDeletionProvider);
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _cancel() async {
    setState(() => _busy = true);
    try {
      await ref.read(accountDataSourceProvider).cancelDeletion();
      ref.invalidate(accountDeletionProvider);
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(accountDeletionProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Delete account')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Padding(padding: const EdgeInsets.all(24), child: Text('$e'))),
        data: (scheduledFor) => ContentWidth(
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: scheduledFor != null
                ? [
                    Text(
                      'Your account is scheduled for deletion on '
                      '${scheduledFor.toLocal().toString().split(' ').first}.',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Nothing has been removed yet. Cancel any time before that date and your account '
                      'carries on as normal.',
                    ),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: _busy ? null : _cancel,
                      child: const Text('Keep my account'),
                    ),
                  ]
                : [
                    const Text(
                      'Deleting removes your profile, posts and comments after a 30-day grace period. '
                      'Your orders, tickets and certificates are kept — we are required to retain those '
                      'records, and a certificate someone else needs to verify has to stay verifiable.',
                    ),
                    const SizedBox(height: 16),
                    TextField(
                      controller: _reason,
                      decoration: const InputDecoration(
                        labelText: 'Why are you leaving? (optional)',
                        border: OutlineInputBorder(),
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _confirm,
                      autocorrect: false,
                      decoration: const InputDecoration(
                        labelText: 'Type DELETE to confirm',
                        border: OutlineInputBorder(),
                      ),
                    ),
                    const SizedBox(height: 16),
                    FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: Theme.of(context).colorScheme.error,
                      ),
                      onPressed: _busy ? null : _request,
                      child: const Text('Schedule deletion'),
                    ),
                  ],
          ),
        ),
      ),
    );
  }
}
