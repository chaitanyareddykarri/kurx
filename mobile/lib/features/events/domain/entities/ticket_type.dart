/// A publicly-listed, on-sale ticket type (`GET /v1/events/{eventId}/ticket-types`).
class TicketType {
  const TicketType({
    required this.id,
    required this.name,
    required this.pricePaise,
    required this.available,
    required this.quantity,
    this.saleEnds,
    this.isAllAccess = false,
    this.pricingUnit,
    this.registrationMode,
    this.groupMin,
    this.groupMax,
    this.priceTiers = const [],
  });

  final String id;
  final String name;

  /// Price in paise (D-004); format with `Money.fromPaise` at the display edge.
  final int pricePaise;
  final int available;
  final int quantity;
  final DateTime? saleEnds;
  final bool isAllAccess;

  /*
   * D-357 — what the price is charged FOR.
   *
   * `pricing_unit` and `registration_mode` have been on this public endpoint since D-020 and this model
   * mapped neither, so the app rendered a bare "₹2,000" that a registrant could not interpret: an entry
   * fee for a whole team, or one member's share of it. Absent reads as PerTicket/Individual, which is
   * what every pre-D-357 ticket type is.
   */
  final String? pricingUnit;
  final String? registrationMode;
  final int? groupMin;
  final int? groupMax;

  /// D-366 — the price bands of a team ticket, ordered by size. Empty for a ticket priced by one
  /// amount, which is what [pricePaise] then means on its own. When bands exist, [pricePaise] is only
  /// the CHEAPEST of them — a "from" figure — and the amount charged is the band the team's size
  /// resolves to, so printing it beside a chosen size would name the wrong number.
  final List<TicketPriceTier> priceTiers;

  bool get isPricedByTeamSize => priceTiers.isNotEmpty;

  bool get soldOut => available <= 0;
  bool get isFree => pricePaise <= 0;

  /// Is this a TEAM entry — one registration covering several people?
  bool get isTeamEntry => registrationMode == 'Group';

  /// Is the price charged once for the whole team, rather than per person?
  bool get isPricedPerTeam => pricingUnit == 'PerGroup';

  /// The price with its unit spelled out in words — never a raw enum, and never a bare amount.
  /// `format` renders paise so this stays free of the money formatter.
  String priceLabel(String Function(int paise) format) {
    // D-366 — bands first: a ticket charging ₹250 for a pair and ₹400 for five has no single price, and
    // quoting the cheapest as if it were THE price is the same defect the unit label exists to prevent.
    if (isPricedByTeamSize) {
      final prices = priceTiers.map((t) => t.pricePaise).toList()..sort();
      return prices.first == prices.last
          ? '${format(prices.first)} per team'
          : '${format(prices.first)}–${format(prices.last)} per team, by size';
    }
    if (isFree) return isTeamEntry ? 'Free per team' : 'Free';
    return isPricedPerTeam ? '${format(pricePaise)} per team' : '${format(pricePaise)} per participant';
  }

  /// D-366 — what a team of [size] pays, or null when no band covers it (the server refuses that
  /// registration with `no_price_for_team_size`) or two do (`ambiguous_price_rule`). Mirrors
  /// `OrderService.TierPriceAsync`, so the app never quotes a price the server will not charge.
  int? priceForTeamSize(int size) {
    if (!isPricedByTeamSize) return pricePaise;
    final matching = priceTiers.where((t) => size >= t.minSize && size <= t.maxSize).toList();
    return matching.length == 1 ? matching.single.pricePaise : null;
  }

  /// The band table as display rows — "3 members" / "₹300" — sorted by size. §21: a registrant must
  /// never be shown a price they cannot interpret.
  List<({String size, int pricePaise})> get teamPriceRows {
    final sorted = [...priceTiers]..sort((a, b) => a.minSize.compareTo(b.minSize));
    return sorted
        .map((t) => (
              size: t.minSize == t.maxSize ? '${t.minSize} members' : '${t.minSize}–${t.maxSize} members',
              pricePaise: t.pricePaise,
            ))
        .toList();
  }

  /// "Teams of 3–5", or null when this is not a team entry or no bounds were set.
  String? get teamSizeLabel {
    if (!isTeamEntry || (groupMin == null && groupMax == null)) return null;
    if (groupMin != null && groupMax != null) {
      return groupMin == groupMax ? 'Teams of $groupMin' : 'Teams of $groupMin–$groupMax';
    }
    return groupMin != null ? 'Teams of $groupMin+' : 'Teams of up to $groupMax';
  }

  /// What `available` counts — team slots for a team entry, places otherwise.
  String get availabilityLabel =>
      soldOut ? 'Sold out' : isTeamEntry ? '$available team slots left' : '$available left';
}

/// D-366 — one team-size price band. Both ends inclusive; the price is for the WHOLE team.
class TicketPriceTier {
  const TicketPriceTier({required this.minSize, required this.maxSize, required this.pricePaise});

  final int minSize;
  final int maxSize;
  final int pricePaise;
}
