import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/common/util/phone_utils.dart';
import 'package:kurx_mobile/common/widgets/phone_field.dart';

void main() {
  // ── Canonical parsing / validation (Phase 6) — the "one strategy" shared with backend + web ──

  group('toE164Identifier', () {
    test('a bare national number defaults to India and becomes E.164', () {
      expect(toE164Identifier('9876543210'), '+919876543210');
      expect(toE164Identifier('98765 43210'), '+919876543210'); // spaces tolerated
    });

    test('already-international numbers pass through as canonical E.164 (multi-country)', () {
      expect(toE164Identifier('+14155552671'), '+14155552671'); // US
      expect(toE164Identifier('+44 7911 123456'), '+447911123456'); // GB (spaces stripped)
      expect(toE164Identifier('+971 50 123 4567'), '+971501234567'); // AE
    });

    test('email and username are returned unchanged (mixed identifier field)', () {
      expect(toE164Identifier('alice@example.com'), 'alice@example.com');
      expect(toE164Identifier('alice_2'), 'alice_2');
      expect(toE164Identifier(''), '');
    });

    test('an unparseable/too-short number is returned as typed — the backend is authoritative', () {
      expect(toE164Identifier('123'), '123');
    });
  });

  group('isValidPhone', () {
    test('accepts valid, rejects invalid — matching libphonenumber', () {
      expect(isValidPhone('+919876543210'), isTrue);
      expect(isValidPhone('+14155552671'), isTrue);
      expect(isValidPhone('+91987'), isFalse); // too short
      expect(isValidPhone('+11'), isFalse);
    });
  });

  group('flagForIso', () {
    test('derives the flag emoji from the ISO code', () {
      expect(flagForIso('IN'), '🇮🇳');
      expect(flagForIso('US'), '🇺🇸');
    });
  });

  // ── PhoneField widget ────────────────────────────────────────────────────────

  Widget host(void Function(String, bool) onChanged) =>
      MaterialApp(home: Scaffold(body: PhoneField(onChanged: onChanged)));

  testWidgets('emits E.164 and valid for a national number (default India)', (tester) async {
    String? e164;
    var valid = false;
    await tester.pumpWidget(host((e, v) {
      e164 = e;
      valid = v;
    }));

    await tester.enterText(find.byType(TextField), '9876543210');
    await tester.pump();

    expect(e164, '+919876543210');
    expect(valid, isTrue);
  });

  testWidgets('pasting an international number re-selects the country', (tester) async {
    String? e164;
    await tester.pumpWidget(host((e, v) => e164 = e));

    await tester.enterText(find.byType(TextField), '+14155552671');
    await tester.pump();

    expect(e164, '+14155552671'); // switched from IN to US on paste
  });

  testWidgets('the country picker opens and is searchable', (tester) async {
    await tester.pumpWidget(host((_, _) {}));

    await tester.tap(find.byType(InkWell).first);
    await tester.pumpAndSettle();

    // Search narrows the (245-country) list to friendly names.
    await tester.enterText(find.widgetWithText(TextField, 'Search country or code'), 'United States');
    await tester.pumpAndSettle();
    expect(find.text('United States'), findsWidgets);
    expect(find.text('India'), findsNothing); // filtered out
  });
}
