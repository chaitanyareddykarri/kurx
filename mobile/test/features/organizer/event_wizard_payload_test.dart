import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/organizer/domain/event_wizard_payload.dart';

/// D-265 — the create-event wizard's payload shaping. Mirrors `web/test/event-wizard.test.ts` case
/// for case: both clients must decide "untouched" identically, or the same wizard clears different
/// fields depending on which device the organiser used.
void main() {
  test('returns null when every field is blank, so an untouched step sends nothing', () {
    // On the server null means "leave alone" and an empty string means "clear". A step the organiser
    // never opened must not clear fields another step set.
    expect(compactGroup({'tagline': '', 'rules': null}), isNull);
  });

  test('keeps only the fields that carry a value', () {
    expect(
      compactGroup({'tagline': 'Ship it', 'shortDescription': '', 'rules': null}),
      {'tagline': 'Ship it'},
    );
  });

  test('trims surrounding whitespace', () {
    expect(compactGroup({'room': '  301  '}), {'room': '301'});
  });

  test('treats a whitespace-only string as empty', () {
    // Otherwise a stray space becomes a "clear this field" instruction.
    expect(compactGroup({'room': '   '}), isNull);
  });

  test('keeps false — an unchecked switch is a real answer, not an absent one', () {
    expect(compactGroup({'autoClose': false}), {'autoClose': false});
  });

  test('keeps zero — a zero value is deliberate', () {
    expect(compactGroup({'minAge': 0}), {'minAge': 0});
  });

  test('drops a null parsed from an empty numeric field', () {
    // `int.tryParse('')` is null, which is how an untouched age input reaches here.
    expect(
      compactGroup({'minAge': int.tryParse(''), 'maxAge': int.tryParse('25')}),
      {'maxAge': 25},
    );
  });

  test('an entirely empty map yields null rather than an empty map', () {
    expect(compactGroup({}), isNull);
  });
}
