import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/events/data/models/event_summary_dto.dart';
import 'package:kurx_mobile/features/events/data/models/ticket_type_dto.dart';

/// D-357 — a price with no unit is ambiguous, and the app rendered exactly that.
///
/// `pricing_unit` / `registration_mode` / `group_min` / `group_max` have been on the public
/// `GET /v1/events/{id}/ticket-types` response since D-020; `TicketTypeDto` parsed them and
/// `toEntity()` dropped all four, so every surface showed a bare "₹2,000". Mirrors web's
/// `priceLabel`/`teamSizeLabel` word for word so the two clients cannot describe one ticket differently.
void main() {
  Map<String, dynamic> ticket({
    int price = 200000,
    String? unit = 'PerGroup',
    String? mode = 'Group',
    int? min = 3,
    int? max = 5,
    int available = 12,
  }) =>
      {
        'id': 't1', 'name': 'Team Entry', 'price_paise': price, 'quantity': 50,
        'sold': 50 - available, 'available': available,
        'pricing_unit': unit, 'registration_mode': mode, 'group_min': min, 'group_max': max,
      };

  String money(int paise) => '₹${(paise / 100).round()}';

  test('a team entry is priced per team, never per head', () {
    final t = TicketTypeDto.fromJson(ticket()).toEntity();
    expect(t.isTeamEntry, isTrue);
    expect(t.isPricedPerTeam, isTrue);
    expect(t.priceLabel(money), '₹2000 per team');
  });

  test('an individual entry is priced per participant', () {
    final t = TicketTypeDto.fromJson(
        ticket(price: 50000, unit: 'PerTicket', mode: 'Individual', min: null, max: null)).toEntity();
    expect(t.isTeamEntry, isFalse);
    expect(t.priceLabel(money), '₹500 per participant');
  });

  test('free still says what it is free FOR', () {
    expect(TicketTypeDto.fromJson(ticket(price: 0)).toEntity().priceLabel(money), 'Free per team');
    expect(
        TicketTypeDto.fromJson(ticket(price: 0, unit: 'PerTicket', mode: 'Individual')).toEntity()
            .priceLabel(money),
        'Free');
  });

  test('the team size a registrant needs before deciding', () {
    expect(TicketTypeDto.fromJson(ticket()).toEntity().teamSizeLabel, 'Teams of 3–5');
    expect(TicketTypeDto.fromJson(ticket(min: 4, max: 4)).toEntity().teamSizeLabel, 'Teams of 4');
    expect(TicketTypeDto.fromJson(ticket(min: 2, max: null)).toEntity().teamSizeLabel, 'Teams of 2+');
    expect(TicketTypeDto.fromJson(ticket(min: null, max: 6)).toEntity().teamSizeLabel,
        'Teams of up to 6');
  });

  test('no team size is shown for an individual ticket', () {
    expect(
        TicketTypeDto.fromJson(ticket(unit: 'PerTicket', mode: 'Individual')).toEntity().teamSizeLabel,
        isNull);
  });

  test('availability counts TEAM SLOTS for a team entry', () {
    // Under PerGroup one team takes one unit, so "12 left" means twelve teams — not twelve people.
    expect(TicketTypeDto.fromJson(ticket()).toEntity().availabilityLabel, '12 team slots left');
    expect(
        TicketTypeDto.fromJson(ticket(unit: 'PerTicket', mode: 'Individual')).toEntity()
            .availabilityLabel,
        '12 left');
  });

  /*
   * D-361 — the discovery CARD.
   *
   * The card is deliberately condensed, but "From ₹2,000" on a team event reads as a per-person
   * minimum when it is the whole team's entry fee. `EventSummary` carried no unit at all, so a card
   * could not have said otherwise even if it wanted to.
   */
  test('a summary carries the unit its "From" price is charged in', () {
    Map<String, dynamic> summary(String? unit) => {
          'id': 's1', 'title': 'Championship', 'slug': 'championship',
          'starts_at': '2026-10-01T10:00:00Z', 'ends_at': '2026-10-01T18:00:00Z',
          'status': 'published', 'price_from_paise': 200000, 'price_from_unit': unit,
        };
    expect(EventSummaryDto.fromJson(summary('PerGroup')).toEntity().isPricedPerTeam, isTrue);
    expect(EventSummaryDto.fromJson(summary('PerTicket')).toEntity().isPricedPerTeam, isFalse);
    // Absent = every pre-D-357 event. Must not become "team" by accident.
    expect(EventSummaryDto.fromJson(summary(null)).toEntity().isPricedPerTeam, isFalse);
  });

  test('a response with none of the fields reads as an individual ticket', () {
    // Every pre-D-357 row. Absent must not become "team" by accident.
    final t = TicketTypeDto.fromJson({
      'id': 't2', 'name': 'General', 'price_paise': 50000, 'quantity': 10, 'sold': 0, 'available': 10,
    }).toEntity();
    expect(t.isTeamEntry, isFalse);
    expect(t.isPricedPerTeam, isFalse);
    expect(t.priceLabel(money), '₹500 per participant');
    expect(t.teamSizeLabel, isNull);
  });

  /// D-366 — a team's price may depend on its size, and one number cannot say so.
  group('team-size price bands', () {
    Map<String, dynamic> banded() => {
          'id': 't3', 'name': 'Team Entry', 'price_paise': 25000, 'quantity': 100, 'sold': 0,
          'available': 100, 'pricing_unit': 'PerGroup', 'registration_mode': 'Group',
          'group_min': 2, 'group_max': 5,
          'price_tiers': [
            {'min_size': 2, 'max_size': 2, 'price_paise': 25000},
            {'min_size': 3, 'max_size': 3, 'price_paise': 30000},
            {'min_size': 4, 'max_size': 5, 'price_paise': 40000},
          ],
        };

    test('the headline becomes a range, not the cheapest band posing as the price', () {
      final t = TicketTypeDto.fromJson(banded()).toEntity();
      expect(t.isPricedByTeamSize, isTrue);
      expect(t.priceLabel(money), '₹250–₹400 per team, by size');
    });

    test('a team of 2 and a team of 3 resolve to different prices', () {
      // The claim the whole decision rests on, asserted on the client too: the app must never quote a
      // price the server will not charge.
      final t = TicketTypeDto.fromJson(banded()).toEntity();
      expect(t.priceForTeamSize(2), 25000);
      expect(t.priceForTeamSize(3), 30000);
      expect(t.priceForTeamSize(4), 40000);
      expect(t.priceForTeamSize(5), 40000);   // the same band covers both
    });

    test('a size no band covers has no price rather than a guessed one', () {
      // The server refuses this registration with `no_price_for_team_size`; quoting the headline here
      // would show a number nobody will be charged.
      final t = TicketTypeDto.fromJson(banded()).toEntity();
      expect(t.priceForTeamSize(9), isNull);
    });

    test('the rows name the size beside every price, sorted', () {
      final rows = TicketTypeDto.fromJson(banded()).toEntity().teamPriceRows;
      expect(rows.map((r) => r.size).toList(), ['2 members', '3 members', '4–5 members']);
      expect(rows.first.pricePaise, 25000);
    });

    test('a ticket with no bands is unchanged — every row that predates D-366', () {
      final t = TicketTypeDto.fromJson({
        'id': 't4', 'name': 'Team', 'price_paise': 200000, 'quantity': 10, 'sold': 0, 'available': 10,
        'pricing_unit': 'PerGroup', 'registration_mode': 'Group', 'group_min': 2, 'group_max': 4,
      }).toEntity();
      expect(t.isPricedByTeamSize, isFalse);
      expect(t.priceLabel(money), '₹2000 per team');
      expect(t.priceForTeamSize(3), 200000);   // one price, every size
    });
  });
}
