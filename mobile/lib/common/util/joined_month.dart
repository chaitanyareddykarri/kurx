import 'package:intl/intl.dart';

/// Renders the public profile's month-precision join date (`"2026-08"`) as `"August 2026"`.
///
/// Built from the parts rather than by parsing a synthesised instant. `DateTime.parse('2026-08')`
/// throws outright, and the obvious workaround — parsing `'2026-08-01'` and formatting it — reads the
/// string as local midnight, which in a negative-offset zone renders the *previous* month. A
/// year-month is a label, not a moment; it is never converted to one and never localised. Web's
/// `formatJoinedMonth` pins the same rule with `timeZone: "UTC"`.
///
/// Returns null for anything that is not `YYYY-MM`, so a caller renders nothing rather than a
/// half-formatted string.
String? formatJoinedMonth(String yearMonth) {
  final parts = yearMonth.split('-');
  if (parts.length < 2) return null;
  final year = int.tryParse(parts[0]);
  final month = int.tryParse(parts[1]);
  if (year == null || month == null || month < 1 || month > 12) return null;
  return '${DateFormat('MMMM', 'en_IN').format(DateTime(year, month))} $year';
}
