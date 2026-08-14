import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:phone_numbers_parser/phone_numbers_parser.dart';

/// The Flutter half of the shared corpus in `docs/api/phone-conformance.json` (D-288).
///
/// The backend and web assert the same file. All three use independent libphonenumber ports whose version
/// numbers cannot be compared to one another, so there is no version to pin — behaviour is the only thing
/// that can be held stable. Without this, a metadata update on one platform silently makes it reject a
/// number the others accept, and the failure surfaces as a user who cannot sign up rather than a red build.
void main() {
  final file = File('../docs/api/phone-conformance.json');
  final fixture = json.decode(file.readAsStringSync()) as Map<String, dynamic>;
  final cases = (fixture['cases'] as List).cast<Map<String, dynamic>>();

  group('cross-platform phone conformance', () {
    test('has a corpus to check', () {
      expect(file.existsSync(), isTrue,
          reason: 'The shared corpus is missing; this test is meaningless without it.');
      expect(cases, isNotEmpty);
    });

    for (final c in cases) {
      final input = c['input'] as String;
      test('$input — ${c['note']}', () {
        PhoneNumber? parsed;
        try {
          parsed = PhoneNumber.parse(input);
        } catch (_) {
          parsed = null;
        }
        final valid = parsed?.isValid() ?? false;

        // If this platform is the outlier, bump its metadata — never edit the fixture to match it.
        expect(valid, c['valid'],
            reason: '$input: expected valid=${c['valid']}, got $valid');
        if (c['valid'] != true) return;

        expect(parsed!.international, c['e164']);
        expect(parsed.isoCode.name, c['region']);
      });
    }
  });
}
