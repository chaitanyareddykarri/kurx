import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/profile_providers.dart';

/// Profile visibility (D-219; four tiers per section, D-221).
///
/// Each section resolves through the backend's central visibility resolver — this screen only picks
/// the tier. Saving immediately per section rather than behind a Save button is deliberate: the
/// endpoint is partial by design, and a privacy control that silently stays unsaved is a bad failure.
class ProfilePrivacyPage extends ConsumerStatefulWidget {
  const ProfilePrivacyPage({super.key});

  @override
  ConsumerState<ProfilePrivacyPage> createState() => _ProfilePrivacyPageState();
}

/// Section key → (label, hint). Order matches the web privacy screen so the two read the same.
const _sections = <(String, String, String)>[
  ('profile', 'My profile page',
      "Below this level your profile is not found at all — it can't be told apart from one that doesn't exist."),
  ('events', 'Events I organized', 'Public events you ran or helped run.'),
  ('attended', 'Events I attended', 'Events you checked in to. Hidden by default.'),
  ('certificates', 'Certificates',
      'Each certificate stays verifiable by its code even when hidden here.'),
  ('achievements', 'Achievements', 'Competition wins and recognitions from verified results.'),
  ('organizations', 'Organizations', 'Verified memberships and the roles you hold.'),
  ('timeline', 'Professional timeline', 'Your chronological milestones.'),
  ('network', 'Connections', 'You can also hide a single connection from the list.'),
  ('metrics', 'Activity metrics', 'Counts, rates and your Event DNA breakdown.'),
  ('contributions', 'Contribution heatmap', 'Your day-by-day activity over the last year.'),
];

const _tiers = <String, String>{
  'public': 'Anyone',
  'connections': 'My connections',
  'event_participants': "People I've shared an event with",
  'only_me': 'Only me',
};

class _ProfilePrivacyPageState extends ConsumerState<ProfilePrivacyPage> {
  String? _saving;

  Future<void> _set(String section, String tier) async {
    setState(() => _saving = section);
    try {
      await ref
          .read(profileControllerProvider.notifier)
          .savePrivacy(sections: {section: tier});
    } on ApiError catch (e) {
      if (mounted) KurxFeedback.error(context, e.userMessage);
    } finally {
      if (mounted) setState(() => _saving = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final privacy = ref.watch(profileControllerProvider).privacy;

    return Scaffold(
      appBar: AppBar(title: const Text('Privacy')),
      body: ContentWidth(
        maxWidth: 560,
        child: ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            Text(
              'Controls who sees each part of your public profile. Everything shown there is built '
              'from verified activity — these settings decide which parts are visible, never what '
              'they say.',
              style: TextStyle(color: c.muted, fontSize: 13.5, height: 1.35),
            ),
            const SizedBox(height: KSpace.lg),
            for (final (key, label, hint) in _sections) ...[
              _SectionTier(
                label: label,
                hint: hint,
                value: privacy.tierFor(key),
                busy: _saving == key,
                onChanged: (tier) => _set(key, tier),
              ),
              const Divider(height: KSpace.lg),
            ],
            const SizedBox(height: KSpace.xl),
          ],
        ),
      ),
    );
  }
}

class _SectionTier extends StatelessWidget {
  const _SectionTier({
    required this.label,
    required this.hint,
    required this.value,
    required this.busy,
    required this.onChanged,
  });

  final String label;
  final String hint;
  final String value;
  final bool busy;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // An unrecognised tier (a newer backend, an older client) renders as-is rather than snapping the
    // dropdown to a wrong value the user never chose.
    final items = {..._tiers, if (!_tiers.containsKey(value)) value: value};

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: TextStyle(color: c.text, fontSize: 15, fontWeight: FontWeight.w600)),
        const SizedBox(height: 2),
        Text(hint, style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.3)),
        const SizedBox(height: KSpace.sm),
        DropdownButtonFormField<String>(
          initialValue: value,
          isExpanded: true,
          items: [
            for (final entry in items.entries)
              DropdownMenuItem(value: entry.key, child: Text(entry.value)),
          ],
          onChanged: busy ? null : (v) { if (v != null && v != value) onChanged(v); },
        ),
      ],
    );
  }
}
