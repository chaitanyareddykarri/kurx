import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/utils/money.dart';

void main() {
  group('Money.fromMinor (D-004: long minor units → display)', () {
    test('whole rupees render without decimals and with en_IN grouping', () {
      expect(Money.fromMinor(500000), '₹5,000');
      expect(Money.fromMinor(12300000), '₹1,23,000');
    });

    test('non-whole amounts keep two decimals', () {
      expect(Money.fromMinor(150), '₹1.50');
      expect(Money.fromMinor(199), '₹1.99');
    });

    test('zero renders as ₹0', () {
      expect(Money.fromMinor(0), '₹0');
    });
  });

  group('Money.fromMinor currency (D-257: the symbol belongs to the amount)', () {
    test('defaults to INR so a payload without a currency is unchanged', () {
      expect(Money.fromMinor(500000), Money.fromMinor(500000, currency: 'INR'));
    });

    test('a mapped code renders its own symbol, not ₹', () {
      expect(Money.fromMinor(500000, currency: 'USD'), r'$5,000');
      expect(Money.fromMinor(250050, currency: 'EUR'), '€2,500.50');
    });

    test('the code is matched case-insensitively', () {
      expect(Money.fromMinor(100, currency: 'usd'), Money.fromMinor(100, currency: 'USD'));
    });

    test('an unmapped code falls back to the code itself rather than a wrong symbol', () {
      expect(Money.fromMinor(120000, currency: 'AUD'), 'AUD 1,200');
    });
  });
}
