/// Payload shaping for the create-event wizard (D-265).
///
/// Kept out of the page so it can be tested without pumping a widget: it encodes a server contract,
/// and getting it wrong corrupts data silently rather than failing loudly.
library;

/// Drops nulls and blank strings, returning null when nothing survives.
///
/// This is not tidiness. On the server a **null field means "leave alone"** and an **empty string
/// means "clear"**, so a wizard step the organiser never opened must send *nothing* — sending its
/// blanks would wipe fields set in a different step.
///
/// `false` and `0` are deliberately kept: an unchecked switch and a zero value are real answers.
/// Strings are trimmed, because a field holding only spaces is a field the organiser left empty.
Map<String, dynamic>? compactGroup(Map<String, dynamic> raw) {
  final out = <String, dynamic>{};
  raw.forEach((key, value) {
    if (value == null) return;
    if (value is String) {
      final trimmed = value.trim();
      if (trimmed.isEmpty) return;
      out[key] = trimmed;
      return;
    }
    out[key] = value;
  });
  return out.isEmpty ? null : out;
}

// ── Per-step validation ───────────────────────────────────────────────────────────────────────────
//
// The rules behind every Continue button in the create-event wizard, kept out of the page for the same
// reason `compactGroup` is: they encode server contracts, and a wrong answer is silent.
//
// `create_event_page.dart` gated exactly two things — `_basicsValid` on Details and `_representingValid`
// on Representing/Pricing — and let every other step through unconditionally, with a comment saying so.
// Details itself checked 4 of the 9 fields it renders. So an organiser could reach "Create draft" with
// an unnamed ticket, an Online event carrying no join link (`online_url_required`), an inverted age
// range (`invalid_age_range`) or a start date in the past, and find out only from the API.
//
// Each function returns field → message and mirrors a refusal the server already makes. Web's
// `validateDetails` / `validatePlace` / … in `web/lib/event-wizard.ts` are the same rules, message for
// message, so the two clients cannot drift.

/// The Details step. `now` is injected so a test can pin the clock and so the caller re-evaluates it on
/// every rebuild — a start time that was valid when it was typed must stop being valid once it passes.
Map<String, String> validateEventDetails({
  required String title,
  required String subtitle,
  required String description,
  required DateTime? startsAt,
  required DateTime? endsAt,
  required String venueName,
  required String city,
  required String venueAddress,
  required String capacity,
  DateTime? now,
}) {
  final clock = now ?? DateTime.now();
  final errors = <String, String>{};

  // A field holding only spaces is a field the organiser left empty.
  if (title.trim().isEmpty) {
    errors['title'] = 'Title is required';
  } else if (title.trim().length < 2) {
    errors['title'] = 'Title must be at least 2 characters';
  }
  if (subtitle.trim().isEmpty) errors['subtitle'] = 'Subtitle is required';
  if (description.trim().isEmpty) errors['description'] = 'Description is required';

  if (startsAt == null) {
    errors['startsAt'] = 'Start date and time is required';
  } else if (startsAt.isBefore(clock)) {
    errors['startsAt'] = 'Start date cannot be in the past';
  }

  if (endsAt == null) {
    errors['endsAt'] = 'End date and time is required';
  } else if (startsAt != null && !endsAt.isAfter(startsAt)) {
    errors['endsAt'] = 'End date and time must be after the start date';
  }

  if (venueName.trim().isEmpty) errors['venueName'] = 'Venue name is required';
  if (city.trim().isEmpty) errors['city'] = 'City is required';
  if (venueAddress.trim().isEmpty) errors['venueAddress'] = 'Venue address is required';

  final cap = capacity.trim();
  if (cap.isEmpty) {
    errors['capacity'] = 'Capacity is required';
  } else if ((int.tryParse(cap) ?? 0) <= 0) {
    errors['capacity'] = 'Capacity must be greater than 0';
  }
  return errors;
}

/// An absolute URL, in the sense the server's `Uri.TryCreate(..., Absolute)` means it. A bare
/// "meet.google.com/x" has no scheme and is not a link.
bool isAbsoluteUrl(String value) {
  final uri = Uri.tryParse(value.trim());
  return uri != null && uri.hasScheme && uri.host.isNotEmpty;
}

/// The Location step. Optional in full **except** the join link, which `ValidateMode` requires for an
/// Online or Hybrid event (`online_url_required`) — the clearest instance of the ungated-step bug, since
/// choosing Online and leaving the link blank walked four more steps before anything objected.
Map<String, String> validateEventPlace({
  required String eventMode,
  required String onlineUrl,
  required String mapsUrl,
}) {
  final errors = <String, String>{};
  final needsLink = eventMode == 'Online' || eventMode == 'Hybrid';

  if (needsLink && onlineUrl.trim().isEmpty) {
    errors['onlineUrl'] = 'A join link is required for an online or hybrid event';
  } else if (onlineUrl.trim().isNotEmpty && !isAbsoluteUrl(onlineUrl)) {
    errors['onlineUrl'] = 'Join link must be a full URL, including https://';
  }
  if (mapsUrl.trim().isNotEmpty && !isAbsoluteUrl(mapsUrl)) {
    errors['mapsUrl'] = 'Google Maps link must be a full URL, including https://';
  }
  return errors;
}

/// The Windows step. Every window is optional and a pair is checked only when BOTH ends are given —
/// one end alone is a legitimate open-ended window. Mirrors `invalid_registration_window` /
/// `invalid_checkin_window`.
Map<String, String> validateEventWindows({
  required DateTime? registrationOpensAt,
  required DateTime? registrationClosesAt,
  required DateTime? checkinOpensAt,
  required DateTime? checkinClosesAt,
}) {
  final errors = <String, String>{};
  if (registrationOpensAt != null &&
      registrationClosesAt != null &&
      !registrationClosesAt.isAfter(registrationOpensAt)) {
    errors['registrationClosesAt'] = 'Registration must close after it opens';
  }
  if (checkinOpensAt != null && checkinClosesAt != null && !checkinClosesAt.isAfter(checkinOpensAt)) {
    errors['checkinClosesAt'] = 'Check-in must close after it opens';
  }
  return errors;
}

/// The Eligibility step. All-optional — an event with no restriction is the normal case — but the values,
/// once given, have ranges: `invalid_age_range`, and a `maxTeams` of 0 that `ApplyFieldGroups` silently
/// DISCARDS, so capping teams at 0 meant no cap and no warning.
Map<String, String> validateEventEligibility({
  required String minAge,
  required String maxAge,
  required String maxTeams,
}) {
  final errors = <String, String>{};
  const ageRange = 'Age must be a whole number between 0 and 120';

  int? bound(String raw, String field) {
    final t = raw.trim();
    if (t.isEmpty) return null;
    final n = int.tryParse(t);
    if (n == null || n < 0 || n > 120) {
      errors[field] = ageRange;
      return null;
    }
    return n;
  }

  final min = bound(minAge, 'minAge');
  final max = bound(maxAge, 'maxAge');
  if (min != null && max != null && max < min) {
    errors['maxAge'] = 'Maximum age must be at least the minimum age';
  }

  final teams = maxTeams.trim();
  if (teams.isNotEmpty && (int.tryParse(teams) ?? 0) <= 0) {
    errors['maxTeams'] = 'Maximum teams must be greater than 0';
  }
  return errors;
}

/// The Legal step. Optional except the consent text, which switching consent on makes required
/// (`consent_text_required`) — acceptance recorded against an empty string is evidence of nothing.
Map<String, String> validateEventLegal({
  required String termsUrl,
  required bool requiresConsent,
  required String consentText,
}) {
  final errors = <String, String>{};
  if (termsUrl.trim().isNotEmpty && !isAbsoluteUrl(termsUrl)) {
    errors['termsUrl'] = 'Terms link must be a full URL, including https://';
  }
  if (requiresConsent && consentText.trim().isEmpty) {
    errors['consentText'] = 'Write the statement registrants must accept, or turn consent off';
  }
  return errors;
}

/// The Pricing step's ticket. Without one the event is unbookable, so all three fields are required
/// here even though `TicketType` is a separate resource — this step is what creates it.
/// D-366 — one team-size price band as the form holds it. Rupees in, paise on the wire.
class TeamPriceBand {
  TeamPriceBand({this.minSize = '', this.maxSize = '', this.priceRupees = ''});
  String minSize;
  String maxSize;
  String priceRupees;
}

/// The registration option created with the event: what people book, HOW they take part, what it costs
/// in that unit, and how many of that unit are available.
///
/// Mirrors web's `validateTicket` — same rules, same order, so an organiser is blocked for the same
/// reasons on both surfaces and the server is refused by neither. `participation` maps to
/// `RegistrationMode` and `PricingUnit` together (D-372): team ⇒ Group + PerGroup (one charge and one
/// inventory unit per team), individual ⇒ Individual + PerTicket.
Map<String, String> validateEventTicket({
  required String name,
  required String priceRupees,
  required String quantity,
  required bool paid,
  String participation = 'individual',
  String teamMin = '',
  String teamMax = '',
  List<TeamPriceBand> bands = const [],
}) {
  final errors = <String, String>{};
  final team = participation == 'team';
  if (name.trim().isEmpty) errors['name'] = 'Name the registration people will book';

  if ((int.tryParse(quantity.trim()) ?? 0) <= 0) {
    // The unit is the point: under PerGroup one team takes exactly one inventory unit, so for a team
    // event this number counts TEAMS and saying "tickets" would describe something else.
    errors['quantity'] = team
        ? 'Set how many teams can enter (at least 1)'
        : 'Set how many places are available (at least 1)';
  }

  // With bands the single price field is not asked at all — the bands ARE the price, and the ticket's
  // headline is derived server-side from the cheapest one (D-366).
  final banded = team && bands.isNotEmpty;
  if (paid && !banded && (double.tryParse(priceRupees.trim()) ?? 0) <= 0) {
    errors['priceRupees'] = 'Set a price above zero, or choose Free';
  }

  if (team) {
    final min = int.tryParse(teamMin.trim());
    final max = int.tryParse(teamMax.trim());
    if (min == null || min < 1) errors['teamMin'] = 'Set the smallest team size (at least 1)';
    if (max == null || max < 1) {
      errors['teamMax'] = 'Set the largest team size (at least 1)';
    } else if (min != null && max < min) {
      errors['teamMax'] = 'The largest team size must be at least the smallest';
    }
    if (bands.isNotEmpty) {
      final bandError = validateTeamPriceBands(bands: bands, teamMin: teamMin, teamMax: teamMax, paid: paid);
      if (bandError != null) errors['bands'] = bandError;
    }
  }
  return errors;
}

/// D-366 — the band set, checked as a SET.
///
/// Mirrors `TicketTypeService.ValidateTiers` and web's `validateTiers`. Every rule here is invisible
/// row by row: individually-sane bands can still leave a size unpriced ("2 and 4–5" refuses every team
/// of 3) or price one twice ("2–4 and 3–5"), and only reading them together shows it. The server is
/// still the authority — this moves the sentence to where the numbers are on screen.
String? validateTeamPriceBands({
  required List<TeamPriceBand> bands,
  required String teamMin,
  required String teamMax,
  required bool paid,
}) {
  // A free event has no bands at all — ₹0 rules would be a price meaning "no price".
  if (!paid) return 'A free event has no prices to set by team size';

  final min = int.tryParse(teamMin.trim());
  final max = int.tryParse(teamMax.trim());
  if (min == null || max == null || min < 1 || max < min) {
    return 'Set the smallest and largest team size first';
  }

  final parsed = <({int min, int max, double price})>[];
  for (final b in bands) {
    final bMin = int.tryParse(b.minSize.trim());
    final bMax = int.tryParse(b.maxSize.trim());
    final price = double.tryParse(b.priceRupees.trim()) ?? 0;
    if (bMin == null || bMax == null || bMin < 1 || bMax < bMin) {
      return 'Each rule needs a team size range, smallest first';
    }
    if (price <= 0) return 'Each rule needs a price above zero';
    if (bMin < min || bMax > max) return 'Keep every rule between $min and $max members';
    parsed.add((min: bMin, max: bMax, price: price));
  }

  parsed.sort((a, b) => a.min.compareTo(b.min));
  for (var i = 1; i < parsed.length; i++) {
    if (parsed[i].min <= parsed[i - 1].max) return 'Two rules cover the same team size';
  }
  if (parsed.first.min != min || parsed.last.max != max) {
    return 'Cover every team size from $min to $max';
  }
  for (var i = 1; i < parsed.length; i++) {
    if (parsed[i].min != parsed[i - 1].max + 1) {
      return 'No price for teams of ${parsed[i - 1].max + 1}';
    }
  }
  return null;
}

/// The bands as the API takes them (D-004: paise on the wire). Empty in, null out — the server treats
/// absent and empty alike, and sending `[]` would claim a set was cleared that never existed.
List<Map<String, dynamic>>? teamPriceBandsPayload(List<TeamPriceBand> bands) {
  if (bands.isEmpty) return null;
  return bands
      .map((b) => {
            'minSize': int.tryParse(b.minSize.trim()) ?? 0,
            'maxSize': int.tryParse(b.maxSize.trim()) ?? 0,
            'pricePaise': ((double.tryParse(b.priceRupees.trim()) ?? 0) * 100).round(),
          })
      .toList();
}

/// The Authorization step (D-351). Each message names the server's own refusal —
/// `authorization_fields_required`, `official_phone_invalid`, `representative_role_other_required`,
/// `letterhead_required` — so the wizard blocks for the reasons the API would.
Map<String, String> validateEventAuthorization({
  required String headName,
  required String headDesignation,
  required String officialEmail,
  required String officialPhone,
  required String? representativeRole,
  required String representativeRoleOther,
  required bool letterAttached,
}) {
  final errors = <String, String>{};
  if (headName.trim().isEmpty) errors['headName'] = 'Name the signatory who authorises this event';
  if (headDesignation.trim().isEmpty) {
    errors['headDesignation'] = "Give the signatory's designation";
  }
  if (officialEmail.trim().isEmpty) {
    errors['officialEmail'] = "Give the organization's official email";
  } else if (!RegExp(r'^[^@\s]+@[^@\s.]+\.[^@\s]+$').hasMatch(officialEmail.trim())) {
    errors['officialEmail'] = 'Enter a valid email address';
  }
  if (officialPhone.trim().isEmpty) {
    errors['officialPhone'] = "Give the organization's official phone";
  } else if (!RegExp(r'^\+[1-9]\d{7,14}$').hasMatch(officialPhone.trim())) {
    // The same shape `EventAuthorizationBodyValidator` matches.
    errors['officialPhone'] = 'Phone must be in international format, e.g. +919876543210';
  }
  if (representativeRole == null || representativeRole.isEmpty) {
    errors['representativeRole'] = 'Choose your role in the organization';
  } else if (representativeRole == 'Other' && representativeRoleOther.trim().isEmpty) {
    errors['representativeRoleOther'] = 'Describe your role, since you chose Other';
  }
  if (!letterAttached) errors['letterFile'] = 'Attach the authorization letter';
  return errors;
}
