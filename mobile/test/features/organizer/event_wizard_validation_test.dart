import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/organizer/domain/event_wizard_payload.dart';

/// Per-step validation for the create-event wizard.
///
/// Mirrors `web/test/event-wizard.test.ts` case for case, because the two clients drive one API: a rule
/// that holds on web and not on mobile means the same organiser is refused on one device and gets a 400
/// on the other.
///
/// The page gated exactly two things before this — `_basicsValid` on Details (4 of the 9 fields it
/// renders) and `_representingValid` on Representing/Pricing — and let every other step through
/// unconditionally. Each case below names the server refusal it now surfaces on the step that asks.

/// Never a literal: a fixed date rots into a failing test the day it goes past.
DateTime _at(Duration fromNow) => DateTime.now().add(fromNow);

Map<String, String> _details({
  String title = 'Hack Day',
  String subtitle = 'A day of building',
  String description = 'Bring a laptop.',
  DateTime? startsAt,
  DateTime? endsAt,
  String venueName = 'Main Hall',
  String city = 'Chennai',
  String venueAddress = '1 Anna Salai',
  String capacity = '100',
  DateTime? now,
}) =>
    validateEventDetails(
      title: title,
      subtitle: subtitle,
      description: description,
      startsAt: startsAt ?? _at(const Duration(days: 30)),
      endsAt: endsAt ?? _at(const Duration(days: 30, hours: 8)),
      venueName: venueName,
      city: city,
      venueAddress: venueAddress,
      capacity: capacity,
      now: now,
    );

void main() {
  group('validateEventDetails', () {
    test('passes a fully completed step', () {
      expect(_details(), isEmpty);
    });

    test('refuses a step carrying only a title', () {
      // The reported defect: title + two dates and nothing else used to leave the step.
      final errors = _details(
          subtitle: '', description: '', venueName: '', city: '', venueAddress: '', capacity: '');
      expect(
        errors.keys.toList()..sort(),
        ['capacity', 'city', 'description', 'subtitle', 'venueAddress', 'venueName'],
      );
    });

    test('treats a whitespace-only field as empty', () {
      expect(_details(city: '   ')['city'], 'City is required');
      expect(_details(venueName: '\t')['venueName'], 'Venue name is required');
      expect(_details(title: '  ')['title'], 'Title is required');
    });

    test('holds the two-character floor the server also holds', () {
      expect(_details(title: 'x')['title'], 'Title must be at least 2 characters');
    });

    test('refuses a capacity that is zero or negative', () {
      expect(_details(capacity: '0')['capacity'], 'Capacity must be greater than 0');
      expect(_details(capacity: '-5')['capacity'], 'Capacity must be greater than 0');
    });

    // The date rules, pinned against an injected clock rather than the wall clock.
    final noon = DateTime(2026, 8, 15, 12);

    test('refuses a start on a past date', () {
      final errors = _details(
          startsAt: DateTime(2026, 8, 14, 18), endsAt: DateTime(2026, 8, 14, 20), now: noon);
      expect(errors['startsAt'], 'Start date cannot be in the past');
    });

    test('refuses a start earlier today, which a date-only check would miss', () {
      final errors = _details(
          startsAt: DateTime(2026, 8, 15, 11, 59), endsAt: DateTime(2026, 8, 15, 18), now: noon);
      expect(errors['startsAt'], 'Start date cannot be in the past');
    });

    test('accepts today at a time still to come', () {
      final errors = _details(
          startsAt: DateTime(2026, 8, 15, 12, 30), endsAt: DateTime(2026, 8, 15, 18), now: noon);
      expect(errors, isEmpty);
    });

    test('refuses an end equal to the start, not only one before it', () {
      final at = _at(const Duration(days: 10));
      expect(_details(startsAt: at, endsAt: at)['endsAt'],
          'End date and time must be after the start date');
    });

    test('invalidates a previously fine end when the start moves past it', () {
      // Nothing touched the end; moving the start is what broke it.
      final errors = _details(startsAt: _at(const Duration(days: 40)));
      expect(errors['endsAt'], 'End date and time must be after the start date');
    });
  });

  group('validateEventPlace', () {
    // D-378 - every field the Mode asks for is required. Helpers keep each test to the ONE field it
    // is about, so a rule that silently stops firing cannot hide behind a neighbour's error.
    Map<String, String> offline({
      String building = 'Block A',
      String floor = '2',
      String room = '204',
      String mapsUrl = 'https://maps.example.com/x',
    }) =>
        validateEventPlace(
          eventMode: 'Offline',
          onlineUrl: '',
          mapsUrl: mapsUrl,
          building: building,
          floor: floor,
          room: room,
          meetingPlatform: '',
          meetingPassword: '',
        );

    Map<String, String> online({
      String mode = 'Online',
      String onlineUrl = 'https://meet.example.com/x',
      String platform = 'Meet',
      String password = 'abc123',
    }) =>
        validateEventPlace(
          eventMode: mode,
          onlineUrl: onlineUrl,
          mapsUrl: '',
          building: '',
          floor: '',
          room: '',
          meetingPlatform: platform,
          meetingPassword: password,
        );

    test('accepts a fully answered in-person event', () => expect(offline(), isEmpty));
    test('accepts a fully answered online event', () => expect(online(), isEmpty));

    test('requires each physical field of an in-person event', () {
      expect(offline(building: '')['building'], 'Building is required');
      expect(offline(floor: '  ')['floor'], 'Floor is required');
      expect(offline(room: '')['room'], 'Room is required');
      expect(offline(mapsUrl: '')['mapsUrl'], 'Google Maps link is required');
    });

    test('asks an in-person event for NOTHING online, and vice versa', () {
      // The conditional is the whole rule: a join link on an in-person event is not missing, it does
      // not exist. Requiring both groups unconditionally would deadlock event creation.
      expect(offline().containsKey('onlineUrl'), isFalse);
      expect(offline().containsKey('meetingPlatform'), isFalse);
      expect(online().containsKey('building'), isFalse);
      expect(online().containsKey('mapsUrl'), isFalse);
    });

    test('requires the join link, platform and password once Online or Hybrid', () {
      for (final mode in ['Online', 'Hybrid']) {
        expect(online(mode: mode, onlineUrl: '')['onlineUrl'],
            'A join link is required for an online or hybrid event');
        expect(online(mode: mode, platform: '')['meetingPlatform'], 'Platform is required');
        expect(online(mode: mode, password: '')['meetingPassword'], 'Meeting password is required');
      }
    });

    test('a Hybrid event is asked for BOTH groups', () {
      final errors = validateEventPlace(
        eventMode: 'Hybrid',
        onlineUrl: '',
        mapsUrl: '',
        building: '',
        floor: '',
        room: '',
        meetingPlatform: '',
        meetingPassword: '',
      );
      expect(errors['building'], isNotNull);
      expect(errors['onlineUrl'], isNotNull);
    });

    test('refuses links with no scheme', () {
      expect(online(onlineUrl: 'meet.example.com')['onlineUrl'],
          'Join link must be a full URL, including https://');
      expect(offline(mapsUrl: 'maps.example.com')['mapsUrl'],
          'Google Maps link must be a full URL, including https://');
    });
  });

  group('validateEventWindows', () {
    Map<String, String> windows({
      DateTime? regOpens,
      DateTime? regCloses,
      DateTime? checkinOpens,
      DateTime? checkinCloses,
      DateTime? result,
      DateTime? cert,
      bool isPrivate = false,
    }) =>
        validateEventWindows(
          registrationOpensAt: regOpens ?? _at(const Duration(days: 1)),
          registrationClosesAt: regCloses ?? _at(const Duration(days: 2)),
          checkinOpensAt: checkinOpens ?? _at(const Duration(days: 3)),
          checkinClosesAt: checkinCloses ?? _at(const Duration(days: 4)),
          resultDate: result ?? _at(const Duration(days: 5)),
          certificateReleaseAt: cert ?? _at(const Duration(days: 6)),
          isPrivate: isPrivate,
        );

    test('accepts a fully answered step', () => expect(windows(), isEmpty));

    test('D-378 - every window is required', () {
      expect(
        validateEventWindows(
            registrationOpensAt: null,
            registrationClosesAt: null,
            checkinOpensAt: null,
            checkinClosesAt: null),
        {
          'registrationOpensAt': 'Registration opening time is required',
          'registrationClosesAt': 'Registration closing time is required',
          'checkinOpensAt': 'Check-in opening time is required',
          'checkinClosesAt': 'Check-in closing time is required',
          'resultDate': 'Results announcement time is required',
          'certificateReleaseAt': 'Certificate release time is required',
        },
      );
    });

    test('a Private event is not asked for results or certificates it never sees', () {
      expect(windows(result: null, cert: null, isPrivate: true), isEmpty);
    });

    test('still orders each pair', () {
      expect(
        windows(
            regOpens: _at(const Duration(days: 5)),
            regCloses: _at(const Duration(days: 4)))['registrationClosesAt'],
        'Registration must close after it opens',
      );
      final same = _at(const Duration(days: 5));
      expect(windows(checkinOpens: same, checkinCloses: same)['checkinClosesAt'],
          'Check-in must close after it opens');
    });
  });

  group('validateEventEligibility', () {
    test('accepts a fully answered step', () {
      expect(validateEventEligibility(minAge: '16', maxAge: '30', maxTeams: '40'), isEmpty);
    });

    test('D-378 - ages and the team cap are required', () {
      expect(validateEventEligibility(minAge: '', maxAge: '', maxTeams: ''), {
        'minAge': 'Minimum age is required',
        'maxAge': 'Maximum age is required',
        'maxTeams': 'Maximum teams is required',
      });
    });

    test('a Private event is not asked for the team cap it never sees', () {
      expect(validateEventEligibility(minAge: '16', maxAge: '30', maxTeams: '', isPrivate: true),
          isEmpty);
    });

    test('refuses a maximum age below the minimum (invalid_age_range)', () {
      expect(validateEventEligibility(minAge: '25', maxAge: '18', maxTeams: '4')['maxAge'],
          'Maximum age must be at least the minimum age');
    });

    test('refuses a team cap of zero, which the server would silently discard', () {
      // ApplyFieldGroups does `MaxTeams <= 0 ? null` - so this used to mean "no cap", silently.
      expect(validateEventEligibility(minAge: '16', maxAge: '30', maxTeams: '0')['maxTeams'],
          'Maximum teams must be greater than 0');
    });
  });

  group('validateEventLegal', () {
    Map<String, String> legal({
      String termsUrl = 'https://example.com/terms',
      String coc = 'Be kind',
      String refund = 'No refunds',
      String cancellation = 'Cancel anytime',
      bool requiresConsent = false,
      String consentText = '',
    }) =>
        validateEventLegal(
          termsUrl: termsUrl,
          requiresConsent: requiresConsent,
          consentText: consentText,
          codeOfConduct: coc,
          refundPolicy: refund,
          cancellationPolicy: cancellation,
        );

    test('accepts a fully answered step', () => expect(legal(), isEmpty));

    test('D-378 - the four policy fields are required', () {
      expect(legal(termsUrl: '')['termsUrl'], 'Terms link is required');
      expect(legal(coc: '  ')['codeOfConduct'], 'Code of conduct is required');
      expect(legal(refund: '')['refundPolicy'], 'Refund policy is required');
      expect(legal(cancellation: '')['cancellationPolicy'], 'Cancellation policy is required');
    });

    test('refuses a terms link with no scheme', () {
      expect(legal(termsUrl: 'example.com/terms')['termsUrl'],
          'Terms link must be a full URL, including https://');
    });

    test('makes the consent text required only once consent is switched on', () {
      expect(legal(requiresConsent: true, consentText: '')['consentText'], isNotNull);
      expect(legal(requiresConsent: true, consentText: '  ')['consentText'], isNotNull);
      expect(legal(requiresConsent: true, consentText: 'I agree'), isEmpty);
    });
  });

  group('validateEventTicket', () {
    test('requires a name and a positive quantity', () {
      final errors =
          validateEventTicket(name: ' ', priceRupees: '', quantity: '0', paid: false);
      expect(errors['name'], isNotNull);
      expect(errors['quantity'], isNotNull);
    });

    test('requires a price above zero only when the event is paid', () {
      expect(
          validateEventTicket(name: 'General', priceRupees: '', quantity: '10', paid: false),
          isEmpty);
      expect(
          validateEventTicket(name: 'General', priceRupees: '', quantity: '10', paid: true)['priceRupees'],
          'Set a price above zero, or choose Free');
    });
  });

  group('validateEventAuthorization', () {
    Map<String, String> auth({
      String phone = '+919876543210',
      String? role = 'Principal',
      String roleOther = '',
      bool letter = true,
    }) =>
        validateEventAuthorization(
          headName: 'R Iyer',
          headDesignation: 'Principal',
          officialEmail: 'head@iitm.ac.in',
          officialPhone: phone,
          representativeRole: role,
          representativeRoleOther: roleOther,
          letterAttached: letter,
        );

    test('passes a complete filing with a letter attached', () {
      expect(auth(), isEmpty);
    });

    test('refuses a phone that is not E.164, matching the server regex', () {
      expect(auth(phone: '9876543210')['officialPhone'],
          'Phone must be in international format, e.g. +919876543210');
    });

    test('makes the free-text role required only when the role is Other', () {
      expect(auth(role: 'Other')['representativeRoleOther'], isNotNull);
      expect(auth(role: 'Other', roleOther: 'Dean'), isEmpty);
    });

    test('refuses a missing letter (letterhead_required)', () {
      expect(auth(letter: false)['letterFile'], 'Attach the authorization letter');
    });
  });

  /// D-372 — the registration UNIT, and D-366 — a price that depends on team size.
  ///
  /// Mirrors `web/test/event-wizard.test.ts` case for case. This app could not create a team
  /// registration at all before now: it sent `PerTicket`/`Individual` as literals, so an organiser on a
  /// phone made an individual-entry event whatever the archetype allowed.
  group('registration', () {
    Map<String, String> ticket({
      String name = 'General',
      String price = '',
      String quantity = '10',
      bool paid = false,
      String participation = 'individual',
      String teamMin = '2',
      String teamMax = '5',
      List<TeamPriceBand> bands = const [],
    }) =>
        validateEventTicket(
          name: name,
          priceRupees: price,
          quantity: quantity,
          paid: paid,
          participation: participation,
          teamMin: teamMin,
          teamMax: teamMax,
          bands: bands,
        );

    List<TeamPriceBand> full() => [
          TeamPriceBand(minSize: '2', maxSize: '2', priceRupees: '250'),
          TeamPriceBand(minSize: '3', maxSize: '3', priceRupees: '300'),
          TeamPriceBand(minSize: '4', maxSize: '5', priceRupees: '400'),
        ];

    test('names the quantity in the unit it is counted in', () {
      // Under PerGroup one team takes one inventory unit, so the number means something different and
      // the message has to say which.
      expect(ticket(quantity: '')['quantity'], contains('places'));
      expect(ticket(quantity: '', participation: 'team')['quantity'], contains('teams'));
    });

    test('asks for team bounds only when the entry is a team', () {
      expect(ticket(teamMin: '', teamMax: ''), isEmpty);
      final errors = ticket(participation: 'team', teamMin: '', teamMax: '');
      expect(errors['teamMin'], isNotNull);
      expect(errors['teamMax'], isNotNull);
    });

    test('refuses a largest team below the smallest', () {
      expect(ticket(participation: 'team', teamMin: '4', teamMax: '2')['teamMax'],
          'The largest team size must be at least the smallest');
    });

    test('requires a price above zero only when the event is paid', () {
      expect(ticket(), isEmpty);
      expect(ticket(paid: true)['priceRupees'], 'Set a price above zero, or choose Free');
    });

    test('accepts bands that cover every allowed size exactly once', () {
      expect(ticket(paid: true, participation: 'team', bands: full()), isEmpty);
    });

    test('stops asking for a single price once bands exist', () {
      // Two price inputs for one decision is the duplicate-question mistake (D-365); with bands the
      // bands ARE the price and the headline is derived server-side from the cheapest.
      expect(ticket(paid: true, participation: 'team', bands: full())['priceRupees'], isNull);
    });

    test('refuses two rules covering the same team size', () {
      expect(
        ticket(paid: true, participation: 'team', bands: [
          TeamPriceBand(minSize: '2', maxSize: '4', priceRupees: '250'),
          TeamPriceBand(minSize: '3', maxSize: '5', priceRupees: '300'),
        ])['bands'],
        'Two rules cover the same team size',
      );
    });

    test('refuses a hole between two rules', () {
      // Teams of 3 are allowed by the ticket and priced by nothing — a refusal at checkout for a size
      // the event advertises.
      expect(
        ticket(paid: true, participation: 'team', bands: [
          TeamPriceBand(minSize: '2', maxSize: '2', priceRupees: '250'),
          TeamPriceBand(minSize: '4', maxSize: '5', priceRupees: '400'),
        ])['bands'],
        'No price for teams of 3',
      );
    });

    test('refuses a set that stops short of the largest team', () {
      expect(
        ticket(paid: true, participation: 'team', bands: [
          TeamPriceBand(minSize: '2', maxSize: '3', priceRupees: '250'),
        ])['bands'],
        'Cover every team size from 2 to 5',
      );
    });

    test('refuses a rule outside the allowed team sizes', () {
      expect(
        ticket(paid: true, participation: 'team', bands: [
          TeamPriceBand(minSize: '2', maxSize: '5', priceRupees: '250'),
          TeamPriceBand(minSize: '6', maxSize: '6', priceRupees: '500'),
        ])['bands'],
        'Keep every rule between 2 and 5 members',
      );
    });

    test('refuses a rule priced at zero', () {
      expect(
        ticket(paid: true, participation: 'team', bands: [
          TeamPriceBand(minSize: '2', maxSize: '5', priceRupees: '0'),
        ])['bands'],
        'Each rule needs a price above zero',
      );
    });

    test('refuses bands on a free event rather than inventing zero-priced rules', () {
      expect(ticket(participation: 'team', bands: full())['bands'],
          'A free event has no prices to set by team size');
    });
  });

  group('teamPriceBandsPayload', () {
    test('converts rupees to paise and sizes to numbers', () {
      expect(
        teamPriceBandsPayload([TeamPriceBand(minSize: '2', maxSize: '3', priceRupees: '250')]),
        [
          {'minSize': 2, 'maxSize': 3, 'pricePaise': 25000}
        ],
      );
    });

    test('sends nothing at all when there are no bands', () {
      // `[]` would claim the organiser cleared a set they never had, and the server treats absent and
      // empty alike — an unbanded ticket's request must stay byte-identical to before.
      expect(teamPriceBandsPayload([]), isNull);
    });
  });
}
