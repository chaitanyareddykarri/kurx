import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:kurx_mobile/common/util/joined_month.dart';

/// The public profile's "Joined Kurx · August 2026" line.
///
/// The bug this guards is a timezone one that never shows up on the developer's machine: the obvious
/// implementation parses `'2026-08-01'` into a *local* DateTime and formats it, which in any
/// negative-offset zone lands on 31 July and renders "July 2026". A year-month is a label, not an
/// instant — so it is built from the parts and never converted to one.
void main() {
  setUpAll(() async => initializeDateFormatting('en_IN'));

  test('renders an ISO year-month as a readable month and year', () {
    expect(formatJoinedMonth('2026-08'), 'August 2026');
    expect(formatJoinedMonth('2026-01'), 'January 2026');
    expect(formatJoinedMonth('2026-12'), 'December 2026');
  });

  test('the first and last months of a year do not slip into the neighbouring one', () {
    // January and December are where an off-by-one timezone shift changes the YEAR too, not just
    // the month — the failure that would be least obvious in a screenshot.
    expect(formatJoinedMonth('2026-01'), isNot(contains('2025')));
    expect(formatJoinedMonth('2026-12'), isNot(contains('2027')));
  });

  test('anything that is not YYYY-MM renders nothing rather than a half-formatted string', () {
    expect(formatJoinedMonth('2026'), isNull);
    expect(formatJoinedMonth(''), isNull);
    expect(formatJoinedMonth('2026-13'), isNull);
    expect(formatJoinedMonth('2026-00'), isNull);
    expect(formatJoinedMonth('not-a-date'), isNull);
  });
}
