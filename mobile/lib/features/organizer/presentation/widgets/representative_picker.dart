import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_content_dto.dart';
import '../providers/event_content_providers.dart';

/// Optionally point an authorization at the signatory's Kurx account.
///
/// **A link, never a grant.** Naming someone here gives them no authority over the event and changes
/// nothing about their account — `IEventAuthority` owns authority (D-269). It only lets a reviewer see
/// that the signatory is a known person rather than a name typed into a form. A signatory who is not on
/// Kurx simply cannot be linked, and the letter stands on its own; the field is optional everywhere.
///
/// Mirrors web's `RepresentativePicker` field for field, including the 8-result page size, so the two
/// surfaces can link the same people. Flutter had no user search of any kind before this, which is why
/// the field existed on web and on neither Flutter screen — creation *or* correction.
class RepresentativePicker extends ConsumerStatefulWidget {
  const RepresentativePicker({super.key, required this.linked, required this.onChanged});

  /// The account currently linked, or null. Owned by the parent so the value survives a rebuild of this
  /// widget and is submitted from the same state the rest of the authorization comes from.
  final UserSearchDto? linked;
  final ValueChanged<UserSearchDto?> onChanged;

  @override
  ConsumerState<RepresentativePicker> createState() => _RepresentativePickerState();
}

class _RepresentativePickerState extends ConsumerState<RepresentativePicker> {
  final _query = TextEditingController();
  Timer? _debounce;
  List<UserSearchDto> _results = const [];
  bool _searching = false;
  String? _error;

  @override
  void dispose() {
    _debounce?.cancel();
    _query.dispose();
    super.dispose();
  }

  /// Debounced: a request per keystroke would rate-limit the caller out of their own form, and the
  /// index is not worth querying until there is enough to match on.
  void _onQueryChanged(String value) {
    _debounce?.cancel();
    final term = value.trim();
    if (term.length < 2) {
      setState(() {
        _results = const [];
        _error = null;
      });
      return;
    }
    _debounce = Timer(const Duration(milliseconds: 300), () => _search(term));
  }

  Future<void> _search(String term) async {
    setState(() {
      _searching = true;
      _error = null;
    });
    try {
      final users = await ref.read(eventContentSourceProvider).searchUsers(term);
      if (!mounted) return;
      setState(() {
        _results = users;
        _searching = false;
      });
    } on ApiError catch (e) {
      if (!mounted) return;
      // Non-fatal by design: the link is optional, so a failed lookup must not block filing the letter.
      setState(() {
        _error = e.userMessage;
        _results = const [];
        _searching = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    if (widget.linked case final linked?) {
      return Container(
        padding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.sm),
        decoration: BoxDecoration(
          border: Border.all(color: c.border),
          borderRadius: BorderRadius.circular(KRadius.md),
        ),
        child: Row(
          children: [
            Icon(Icons.alternate_email, size: 16, color: c.muted),
            const SizedBox(width: KSpace.xs),
            Expanded(
              child: Text('@${linked.username}',
                  overflow: TextOverflow.ellipsis, style: TextStyle(color: c.text, fontSize: 14)),
            ),
            IconButton(
              tooltip: 'Unlink this account',
              icon: const Icon(Icons.close, size: 18),
              onPressed: () {
                _query.clear();
                setState(() => _results = const []);
                widget.onChanged(null);
              },
            ),
          ],
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: _query,
          onChanged: _onQueryChanged,
          decoration: InputDecoration(
            labelText: 'Their Kurx account (optional)',
            hintText: 'Search by name or username',
            prefixIcon: const Icon(Icons.search, size: 18),
            suffixIcon: _searching
                ? const Padding(
                    padding: EdgeInsets.all(KSpace.sm),
                    child: SizedBox(
                        width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2)),
                  )
                : null,
          ),
        ),
        if (_error != null) ...[
          const SizedBox(height: KSpace.xs),
          Text(_error!, style: TextStyle(color: c.danger, fontSize: 12.5)),
        ],
        for (final u in _results)
          ListTile(
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: Icon(Icons.alternate_email, size: 16, color: c.muted),
            title: Text(u.name.isEmpty ? '@${u.username}' : u.name,
                style: TextStyle(color: c.text, fontSize: 14)),
            subtitle: Text('@${u.username}', style: TextStyle(color: c.muted, fontSize: 12.5)),
            onTap: () {
              _query.clear();
              setState(() => _results = const []);
              widget.onChanged(u);
            },
          ),
      ],
    );
  }
}
