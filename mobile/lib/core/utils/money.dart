import 'package:intl/intl.dart';

/// Money is `long` paise on the wire (D-004), and every amount travels with the currency it is
/// denominated in (V3 §9.1, D-257) — an event settles in its own currency, so the symbol belongs to
/// the amount and is never a constant. Convert and format only at the display edge; never store or
/// compute in floating-point.
class Money {
  const Money._();

  /// Rendered symbol per ISO-4217 code. An unmapped code falls back to the code itself
  /// ("AUD 1,200") — a plainly-labelled amount is recoverable, a confidently wrong symbol is not.
  static const _symbols = <String, String>{
    'INR': '₹',
    'USD': r'$',
    'EUR': '€',
    'GBP': '£',
    'AED': 'AED ',
    'SGD': r'S$',
  };

  /// [minor] is the amount in the currency's minor unit (paise for INR, cents for USD).
  ///
  /// Every currency the platform settles in today is 100-based. A zero-decimal currency (JPY, KRW)
  /// would divide by 1 rather than 100, and needs an explicit entry here before it can be shown.
  static String fromMinor(int minor, {String currency = 'INR'}) {
    final code = currency.trim().toUpperCase();
    final digits = minor % 100 == 0 ? 0 : 2;
    return NumberFormat.currency(
      locale: 'en_IN',
      symbol: _symbols[code] ?? '$code ',
      decimalDigits: digits,
    ).format(minor / 100.0);
  }
}

/// Top-level shorthand for display-edge money formatting.
///
/// [currency] defaults to INR so a call site whose payload does not carry a currency yet renders
/// exactly as it did before; pass it through wherever the server sends one.
String formatPaise(int minor, {String currency = 'INR'}) =>
    Money.fromMinor(minor, currency: currency);
