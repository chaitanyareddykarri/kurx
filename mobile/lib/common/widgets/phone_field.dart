import 'package:flutter/material.dart';
import 'package:phone_numbers_parser/phone_numbers_parser.dart';

import '../../core/theme/design_tokens.dart';
import '../data/country_names.dart';
import '../util/phone_utils.dart';

/// Shared E.164 phone input for mobile (Phase 6, D-089) — the Dart twin of the web/admin `@kurx/ui`
/// `PhoneField`. A country picker (flag + dial code, searchable) plus a national-number field that
/// formats as you type and emits the canonical **E.164** value, validated with `phone_numbers_parser`
/// (the same libphonenumber family as the backend). The backend remains authoritative; this is
/// fail-fast UX. One widget, used by every phone-only authentication surface.
class PhoneField extends StatefulWidget {
  const PhoneField({
    super.key,
    required this.onChanged,
    this.initialValue,
    this.defaultCountry = IsoCode.IN,
    this.label = 'Phone number',
    this.enabled = true,
    this.autofocus = false,
  });

  /// Emits the E.164 string and whether the parser considers it valid and complete.
  final void Function(String e164, bool valid) onChanged;

  /// Optional initial E.164 value (e.g. carried from a mixed identifier field).
  final String? initialValue;
  final IsoCode defaultCountry;
  final String label;
  final bool enabled;
  final bool autofocus;

  @override
  State<PhoneField> createState() => _PhoneFieldState();
}

class _PhoneFieldState extends State<PhoneField> {
  late IsoCode _country;
  final _controller = TextEditingController();

  @override
  void initState() {
    super.initState();
    _country = widget.defaultCountry;
    final seed = widget.initialValue?.trim();
    if (seed != null && seed.startsWith('+')) {
      try {
        final p = PhoneNumber.parse(seed);
        _country = p.isoCode;
        _controller.text = p.formatNsn();
      } catch (_) {/* keep default */}
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  String _dial(IsoCode iso) {
    try {
      return PhoneNumber.parse('0', callerCountry: iso).countryCode;
    } catch (_) {
      return '';
    }
  }

  void _emit(String digits) {
    if (digits.isEmpty) {
      widget.onChanged('', false);
      return;
    }
    try {
      final p = PhoneNumber.parse(digits, callerCountry: _country);
      widget.onChanged(p.international, p.isValid());
    } catch (_) {
      widget.onChanged('+${_dial(_country)}$digits', false);
    }
  }

  void _onChanged(String raw) {
    final trimmed = raw.trim();
    // Pasting a full international number re-selects the country.
    if (trimmed.startsWith('+')) {
      try {
        final p = PhoneNumber.parse(trimmed);
        final nsn = p.formatNsn();
        setState(() => _country = p.isoCode);
        _controller.value = TextEditingValue(text: nsn, selection: TextSelection.collapsed(offset: nsn.length));
        widget.onChanged(p.international, p.isValid());
        return;
      } catch (_) {/* fall through */}
    }

    final digits = raw.replaceAll(RegExp(r'[^\d]'), '');
    var formatted = digits;
    try {
      if (digits.isNotEmpty) formatted = PhoneNumber.parse(digits, callerCountry: _country).formatNsn();
    } catch (_) {
      formatted = digits;
    }
    if (formatted != _controller.text) {
      _controller.value = TextEditingValue(text: formatted, selection: TextSelection.collapsed(offset: formatted.length));
    }
    _emit(digits);
  }

  Future<void> _pickCountry() async {
    final picked = await showModalBottomSheet<IsoCode>(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _CountrySheet(),
    );
    if (picked != null && mounted) {
      setState(() => _country = picked);
      _emit(_controller.text.replaceAll(RegExp(r'[^\d]'), ''));
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            InkWell(
              onTap: widget.enabled ? _pickCountry : null,
              borderRadius: BorderRadius.circular(KRadius.md),
              child: Container(
                height: 56,
                padding: const EdgeInsets.symmetric(horizontal: 12),
                decoration: BoxDecoration(
                  border: Border.all(color: c.border),
                  borderRadius: BorderRadius.circular(KRadius.md),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(flagForIso(_country.name), style: const TextStyle(fontSize: 18)),
                    const SizedBox(width: 6),
                    Text('+${_dial(_country)}', style: TextStyle(color: c.muted, fontWeight: FontWeight.w600)),
                    Icon(Icons.arrow_drop_down, color: c.muted),
                  ],
                ),
              ),
            ),
            const SizedBox(width: KSpace.sm),
            Expanded(
              child: TextField(
                controller: _controller,
                enabled: widget.enabled,
                autofocus: widget.autofocus,
                keyboardType: TextInputType.phone,
                autofillHints: const [AutofillHints.telephoneNumberNational],
                onChanged: _onChanged,
                decoration: InputDecoration(labelText: widget.label),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

/// The searchable country list. Built once from every ISO code the parser knows; dial codes and flags
/// are derived, names come from [countryNames] with an ISO-code fallback.
class _CountrySheet extends StatefulWidget {
  const _CountrySheet();

  @override
  State<_CountrySheet> createState() => _CountrySheetState();
}

class _Country {
  const _Country(this.iso, this.name, this.dial);
  final IsoCode iso;
  final String name;
  final String dial;
}

List<_Country>? _cachedCountries;

List<_Country> _allCountries() {
  if (_cachedCountries != null) return _cachedCountries!;
  final list = <_Country>[];
  for (final iso in IsoCode.values) {
    String dial;
    try {
      dial = PhoneNumber.parse('0', callerCountry: iso).countryCode;
    } catch (_) {
      continue; // no dial code → not selectable
    }
    list.add(_Country(iso, countryNames[iso.name] ?? iso.name, dial));
  }
  list.sort((a, b) => a.name.compareTo(b.name));
  return _cachedCountries = list;
}

class _CountrySheetState extends State<_CountrySheet> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final q = _query.trim().toLowerCase();
    final all = _allCountries();
    final filtered = q.isEmpty
        ? all
        : all
            .where((x) =>
                x.name.toLowerCase().contains(q) ||
                x.iso.name.toLowerCase() == q ||
                x.dial.startsWith(q.replaceAll('+', '')))
            .toList();

    return DraggableScrollableSheet(
      initialChildSize: 0.7,
      maxChildSize: 0.9,
      expand: false,
      builder: (context, scrollController) => Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(KSpace.md),
            child: TextField(
              autofocus: true,
              onChanged: (v) => setState(() => _query = v),
              decoration: const InputDecoration(
                labelText: 'Search country or code',
                prefixIcon: Icon(Icons.search),
              ),
            ),
          ),
          Expanded(
            child: ListView.builder(
              controller: scrollController,
              itemCount: filtered.length,
              itemBuilder: (context, i) {
                final country = filtered[i];
                return ListTile(
                  leading: Text(flagForIso(country.iso.name), style: const TextStyle(fontSize: 22)),
                  title: Text(country.name),
                  trailing: Text('+${country.dial}', style: TextStyle(color: c.muted)),
                  onTap: () => Navigator.of(context).pop(country.iso),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
