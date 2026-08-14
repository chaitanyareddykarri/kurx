import 'dart:convert';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/content_width.dart';
import '../../../../common/widgets/kurx_avatar.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../common/widgets/kurx_text_field.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../providers/profile_providers.dart';

/// Edit the self-declared half of the profile (D-219). Everything on this screen is authority-zero
/// display data — verified activity (events, certificates, organizations) is never editable and is
/// deliberately absent here.
///
/// Prefilled from `GET /v1/me`, the caller's own record. That matters: reading these fields from the
/// *public* profile fails for an unclaimed username or a private profile, and a form that prefills
/// from a failed read submits blanks over real data.
class EditProfilePage extends ConsumerStatefulWidget {
  const EditProfilePage({super.key});

  @override
  ConsumerState<EditProfilePage> createState() => _EditProfilePageState();
}

/// The four link slots the profile renders, matching the web form and the stored jsonb shape.
const _linkSlots = ['github', 'linkedin', 'website', 'instagram'];

const _maxImageBytes = 5 * 1024 * 1024;

class _EditProfilePageState extends ConsumerState<EditProfilePage> {
  late final TextEditingController _name;
  late final TextEditingController _headline;
  late final TextEditingController _bio;
  late final TextEditingController _skills;
  late final TextEditingController _languages;
  late final TextEditingController _interests;
  // Education is five fields over one jsonb array entry — the entry the public profile renders.
  late final TextEditingController _eduInstitute;
  late final TextEditingController _eduDegree;
  late final TextEditingController _eduBranch;
  late final TextEditingController _eduStartYear;
  late final TextEditingController _eduEndYear;
  late final Map<String, TextEditingController> _links;

  String? _avatarKey;
  DateTime? _dateOfBirth;
  bool _uploading = false;
  bool _saving = false;

  /// Mirrors `Kurx.Domain.Onboarding.MinimumAgeYears` — the picker's ceiling. The server re-validates,
  /// so this only spares the user a rejected save.
  static const _minimumAgeYears = 13;

  Future<void> _pickDateOfBirth() async {
    final today = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? DateTime(today.year - 20, today.month, today.day),
      firstDate: DateTime(today.year - 120),
      lastDate: DateTime(today.year - _minimumAgeYears, today.month, today.day),
      helpText: 'Your date of birth',
      initialDatePickerMode: DatePickerMode.year,
    );
    if (picked != null && mounted) setState(() => _dateOfBirth = picked);
  }

  @override
  void initState() {
    super.initState();
    final p = ref.read(profileControllerProvider);
    _name = TextEditingController(text: p.name);
    _headline = TextEditingController(text: p.headline ?? '');
    _bio = TextEditingController(text: p.bio ?? '');
    _skills = TextEditingController(text: p.skills.join(', '));
    _languages = TextEditingController(text: p.languages.join(', '));
    _interests = TextEditingController(text: p.interests.join(', '));
    final edu = _parseEducation(p.educationJson);
    _dateOfBirth = p.dateOfBirth;
    _eduInstitute = TextEditingController(text: edu.institute);
    _eduDegree = TextEditingController(text: edu.degree);
    _eduBranch = TextEditingController(text: edu.branch);
    _eduStartYear = TextEditingController(text: edu.startYear);
    _eduEndYear = TextEditingController(text: edu.endYear);
    _avatarKey = p.avatarKey;

    final existing = _decodeLinks(p.linksJson);
    _links = {
      for (final slot in _linkSlots) slot: TextEditingController(text: existing[slot] ?? ''),
    };
  }

  @override
  void dispose() {
    _name.dispose();
    _headline.dispose();
    _bio.dispose();
    _skills.dispose();
    _languages.dispose();
    _interests.dispose();
    _eduInstitute.dispose();
    _eduDegree.dispose();
    _eduBranch.dispose();
    _eduStartYear.dispose();
    _eduEndYear.dispose();
    for (final c in _links.values) {
      c.dispose();
    }
    super.dispose();
  }

  /// A malformed stored value yields empty fields rather than throwing the screen away — the column is
  /// free-form jsonb and may predate this shape.
  Map<String, String> _decodeLinks(String? raw) {
    if (raw == null || raw.isEmpty) return const {};
    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map) return const {};
      return {
        for (final e in decoded.entries)
          if (e.value is String) '${e.key}': e.value as String,
      };
    } catch (_) {
      return const {};
    }
  }

  /// Returns the encoded links, or null when one isn't a usable URL (the caller aborts the save).
  String? _encodeLinks() {
    final out = <String, String>{};
    for (final slot in _linkSlots) {
      final raw = _links[slot]!.text.trim();
      if (raw.isEmpty) continue;
      final withScheme = raw.startsWith('http://') || raw.startsWith('https://') ? raw : 'https://$raw';
      final uri = Uri.tryParse(withScheme);
      if (uri == null || !uri.hasAuthority) return null;
      out[slot] = withScheme;
    }
    return jsonEncode(out);
  }

  Future<void> _pickAvatar() async {
    final picked = await FilePicker.pickFiles(type: FileType.image, withData: true);
    final file = picked?.files.firstOrNull;
    final bytes = file?.bytes;
    if (bytes == null) return;

    if (bytes.length > _maxImageBytes) {
      if (mounted) KurxFeedback.error(context, 'Image must be under 5 MB.');
      return;
    }

    setState(() => _uploading = true);
    try {
      final key = await ref.read(profileControllerProvider.notifier).uploadImage(
            slot: 'avatar',
            contentType: _contentTypeFor(file!.extension),
            bytes: bytes,
          );
      if (!mounted) return;
      // Held locally until Save — the key is only persisted with the rest of the form, so backing out
      // of this screen leaves the stored profile untouched.
      setState(() => _avatarKey = key);
    } on ApiError catch (e) {
      if (mounted) KurxFeedback.error(context, e.userMessage);
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  String _contentTypeFor(String? extension) => switch (extension?.toLowerCase()) {
        'png' => 'image/png',
        'webp' => 'image/webp',
        'gif' => 'image/gif',
        'avif' => 'image/avif',
        _ => 'image/jpeg',
      };

  /// Reads the first entry of the `education_json` array — the one the public profile renders. A
  /// malformed or empty value yields blank fields rather than throwing the form away.
  static ({String institute, String degree, String branch, String startYear, String endYear})
      _parseEducation(String? raw) {
    const empty = (institute: '', degree: '', branch: '', startYear: '', endYear: '');
    if (raw == null || raw.trim().isEmpty) return empty;
    try {
      final decoded = jsonDecode(raw);
      final first = decoded is List ? (decoded.isEmpty ? null : decoded.first) : decoded;
      if (first is! Map) return empty;
      String str(Object? v) => v == null ? '' : v.toString();
      return (
        institute: str(first['institute']),
        degree: str(first['degree']),
        branch: str(first['branch']),
        startYear: str(first['start_year']),
        endYear: str(first['end_year']),
      );
    } catch (_) {
      return empty;
    }
  }

  /// Serialises the education fields into the existing `education_json` array.
  ///
  /// **One entry, not a list** — mirrors `web/lib/profile-actions.ts` exactly. The column has always
  /// been an array, but the public profile reads only its first element, so an editor writing
  /// several would silently show one. A repeater belongs with a read side that can display it.
  ///
  /// Returns null when the years are invalid, so the caller refuses rather than storing nonsense.
  /// All-blank writes an empty array: a profile with a nameless institute is worse than one with no
  /// education section.
  String? _buildEducationJson() {
    final institute = _eduInstitute.text.trim();
    final degree = _eduDegree.text.trim();
    final branch = _eduBranch.text.trim();
    final startYear = _eduStartYear.text.trim();
    final endYear = _eduEndYear.text.trim();

    if ([institute, degree, branch, startYear, endYear].every((v) => v.isEmpty)) return '[]';

    // Years are optional, but a year that is present must be a real one — `1` and `20244` are both
    // refused rather than stored and rendered.
    int? parsed;
    bool invalid = false;
    int? year(String raw) {
      if (raw.isEmpty) return null;
      if (!RegExp(r'^\d{4}$').hasMatch(raw)) {
        invalid = true;
        return null;
      }
      parsed = int.parse(raw);
      if (parsed! < 1900 || parsed! > 2100) {
        invalid = true;
        return null;
      }
      return parsed;
    }

    final from = year(startYear);
    final to = year(endYear);
    if (invalid) return null;
    if (from != null && to != null && to < from) return null;

    return jsonEncode([
      {
        'institute': institute.isEmpty ? null : institute,
        'degree': degree.isEmpty ? null : degree,
        'branch': branch.isEmpty ? null : branch,
        'start_year': from,
        'end_year': to,
      }
    ]);
  }

  /// One parser for all three comma-separated lists. They have identical semantics, and three
  /// copies of the same split/trim/filter is how one of them ends up behaving differently.
  static List<String> _csv(TextEditingController c) =>
      c.text.split(',').map((s) => s.trim()).where((s) => s.isNotEmpty).toList();

  Future<void> _save() async {
    final name = _name.text.trim();
    if (name.isEmpty) {
      KurxFeedback.error(context, "Name can't be empty.");
      return;
    }
    final links = _encodeLinks();
    if (links == null) {
      KurxFeedback.error(context, 'Links must be valid URLs.');
      return;
    }

    final education = _buildEducationJson();
    if (education == null) {
      KurxFeedback.error(
        context,
        'Education years must be four-digit years, and the end year cannot precede the start.',
      );
      return;
    }

    setState(() => _saving = true);
    try {
      await ref.read(profileControllerProvider.notifier).saveDisplayProfile(
            name: name,
            headline: _headline.text.trim(),
            bio: _bio.text.trim(),
            skills: _csv(_skills),
            languages: _csv(_languages),
            interests: _csv(_interests),
            dateOfBirth: _dateOfBirth,
            educationJson: education,
            linksJson: links,
            avatarKey: _avatarKey,
          );
      if (!mounted) return;
      KurxFeedback.success(context, 'Profile saved.');
      context.pop();
    } on ApiError catch (e) {
      if (mounted) KurxFeedback.error(context, e.userMessage);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    return Scaffold(
      appBar: AppBar(title: const Text('Edit profile')),
      body: ContentWidth(
        maxWidth: 520,
        child: ListView(
          padding: const EdgeInsets.all(KSpace.lg),
          children: [
            Center(
              child: Column(
                children: [
                  KurxAvatar(name: _name.text, imageUrl: _avatarKey, size: 88),
                  const SizedBox(height: KSpace.sm),
                  TextButton(
                    onPressed: _uploading ? null : _pickAvatar,
                    child: Text(_uploading ? 'Uploading…' : 'Change photo'),
                  ),
                ],
              ),
            ),
            const SizedBox(height: KSpace.md),
            KurxTextField(controller: _name, label: 'Name', textInputAction: TextInputAction.next),
            const SizedBox(height: KSpace.md),
            KurxTextField(
              controller: _headline,
              label: 'Headline',
              hint: 'e.g. Event organizer & speaker',
              textInputAction: TextInputAction.next,
            ),
            const SizedBox(height: KSpace.md),
            KurxTextField(controller: _bio, label: 'About', hint: 'A short bio', maxLines: 4),
            const SizedBox(height: KSpace.md),
            InkWell(
              onTap: _saving ? null : _pickDateOfBirth,
              borderRadius: BorderRadius.circular(KRadius.sm),
              child: InputDecorator(
                decoration: const InputDecoration(
                  labelText: 'Date of birth',
                  border: OutlineInputBorder(),
                ),
                child: Row(
                  children: [
                    Icon(Icons.cake_outlined, size: 20, color: context.kurx.muted),
                    const SizedBox(width: KSpace.sm),
                    Expanded(
                      child: Text(
                        _dateOfBirth == null
                            ? 'Not set'
                            : DateFormat('d MMMM yyyy', 'en_IN').format(_dateOfBirth!),
                        style: TextStyle(
                          color: _dateOfBirth == null ? context.kurx.muted : context.kurx.text,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: KSpace.md),
            KurxTextField(
              controller: _eduInstitute,
              label: 'Institute',
              hint: 'Shown as self-declared, not a verified affiliation',
              textInputAction: TextInputAction.next,
            ),
            KurxTextField(
              controller: _eduDegree,
              label: 'Degree',
              hint: 'e.g. B.Tech',
              textInputAction: TextInputAction.next,
            ),
            KurxTextField(
              controller: _eduBranch,
              label: 'Branch',
              hint: 'e.g. Computer Science',
              textInputAction: TextInputAction.next,
            ),
            Row(
              children: [
                Expanded(
                  child: KurxTextField(
                    controller: _eduStartYear,
                    label: 'From',
                    keyboardType: TextInputType.number,
                    textInputAction: TextInputAction.next,
                  ),
                ),
                const SizedBox(width: KSpace.md),
                Expanded(
                  child: KurxTextField(
                    controller: _eduEndYear,
                    label: 'To',
                    keyboardType: TextInputType.number,
                    textInputAction: TextInputAction.next,
                  ),
                ),
              ],
            ),
            KurxTextField(
              controller: _languages,
              label: 'Languages',
              hint: 'Comma-separated, e.g. English, Hindi, Telugu',
              textInputAction: TextInputAction.next,
            ),
            KurxTextField(
              controller: _interests,
              label: 'Interests',
              // Named apart from Event DNA deliberately: this is what you say you care about, that
              // is what you provably did. The profile shows both and never merges them.
              hint: 'Comma-separated — shown separately from your Event DNA',
              textInputAction: TextInputAction.next,
            ),
            KurxTextField(
              controller: _skills,
              label: 'Skills',
              hint: 'Comma-separated, e.g. Flutter, Public speaking',
            ),
            const SizedBox(height: KSpace.lg),
            Text('Links', style: TextStyle(color: c.text, fontSize: 15, fontWeight: FontWeight.w700)),
            const SizedBox(height: KSpace.sm),
            for (final slot in _linkSlots) ...[
              KurxTextField(
                controller: _links[slot]!,
                label: slot[0].toUpperCase() + slot.substring(1),
                keyboardType: TextInputType.url,
              ),
              const SizedBox(height: KSpace.md),
            ],
            const SizedBox(height: KSpace.sm),
            KurxButton(
              label: _saving ? 'Saving…' : 'Save profile',
              expand: true,
              onPressed: _saving || _uploading ? null : _save,
            ),
            const SizedBox(height: KSpace.xl),
          ],
        ),
      ),
    );
  }
}
