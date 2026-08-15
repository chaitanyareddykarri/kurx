/// The authenticated user, as returned by `GET /v1/me`.
class CurrentUser {
  const CurrentUser({
    required this.id,
    required this.phone,
    required this.name,
    this.username,
    this.email,
    this.emailVerified = false,
    required this.needsOnboarding,
    this.dateOfBirth,
    this.createdAt,
    this.headline,
    this.bio,
    this.skills = const [],
    this.languages = const [],
    this.interests = const [],
    this.educationJson,
    this.linksJson,
    this.avatarKey,
    this.coverKey,
    this.privacy = const ProfilePrivacy(),
    this.trust = const TrustCapabilities(),
  });

  final String id;
  final String phone;
  final String name;
  final String? username;
  final String? email;

  /// Proven by an emailed one-time code, not merely present. A verified email is a recovery and
  /// notification channel, never on its own an authentication factor (D-182).
  final bool emailVerified;

  /// `needs_onboarding: true` — required first-time setup is unfinished: a blank name, no username,
  /// no date of birth, or no password (D-311). Server-derived; never inferred from empty fields here.
  final bool needsOnboarding;

  /// Date-only. Collected during onboarding and editable afterwards; the only fact on the account an
  /// age-restricted event's MinAge/MaxAge can read. Self-declared, never proof of age (M3 does that).
  final DateTime? dateOfBirth;

  /// When the account was created — the "member since" fact.
  final DateTime? createdAt;

  // ── Self-declared display fields (D-219) ──────────────────────────────────
  // Served by `/v1/me` so the editor prefills from the caller's own record. Reading them from the
  // public profile instead is what let a failed read submit blanks over real data on web.
  final String? headline;
  final String? bio;
  final List<String> skills;

  /// Self-declared. Distinct from Event DNA, which is derived from real participation.
  final List<String> languages;
  final List<String> interests;

  /// jsonb array of education entries; the public profile renders its first element.
  final String? educationJson;
  final String? linksJson;
  final String? avatarKey;
  final String? coverKey;
  final ProfilePrivacy privacy;

  /// Live capability flags (M7). Read from the server every request rather than cached, so a
  /// revoked verification stops granting access on the next call rather than at the next sign-in.
  final TrustCapabilities trust;
}

/// What this account is currently allowed to do, derived server-side from verification and fraud
/// state (M7, D-046). **Never re-derived on the client** — the same values gate the money path in
/// `OrderService`, so a local guess that disagreed would either block a legitimate sale or promise
/// one the server refuses. Defaults are the closed position.
class TrustCapabilities {
  const TrustCapabilities({
    this.level = 'L1',
    this.canOrganizeFree = true,
    this.canOrganizePaid = false,
    this.canReceivePayout = false,
    this.identityVerified = false,
    this.bankVerified = false,
    this.canCreatePublicEvent = false,
    this.canCreatePrivateEvent = true,
    this.requiresRepresentation = true,
  });

  final String level;
  final bool canOrganizeFree;

  /// Requires identity verified AND bank verified AND fraud-clear. Selling a ticket additionally
  /// requires the represented organization to be verified — that half is org-scoped and lives in
  /// the workspace capability contract, not here.
  final bool canOrganizePaid;
  final bool canReceivePayout;
  final bool identityVerified;
  final bool bankVerified;

  /// D-307 — may this person create a **public** event, free or paid. Distinct from
  /// [canOrganizePaid] (*may they take money*) even though the predicates are identical today.
  /// Defaults **false**: the closed position, so a response that omits it never reads as permission.
  final bool canCreatePublicEvent;

  /// D-353/D-352 — whether a PUBLIC event must name a verified organisation. True in Production,
  /// always; false only under the dev bypass. Defaults true, the closed position.
  final bool requiresRepresentation;

  /// Constant true server-side — a Private event cannot be Listed, take payment, or reach any
  /// discovery surface, so there is nothing to verify. Defaults true so an older backend does not
  /// accidentally block Private.
  final bool canCreatePrivateEvent;
}

/// Profile visibility (D-219; per-section four-tier in D-221). The four booleans are retained for
/// backward compatibility; [sections] is the richer view and is what the privacy screen edits.
/// Defaults mirror the server's column defaults, so a client talking to an older backend that omits
/// the block behaves exactly as before.
class ProfilePrivacy {
  const ProfilePrivacy({
    this.profilePublic = true,
    this.showAttended = false,
    this.showCertificates = true,
    this.showAllies = true,
    this.sections = const {},
  });

  final bool profilePublic;
  final bool showAttended;
  final bool showCertificates;
  final bool showAllies;

  /// Section key → tier (`public` | `connections` | `event_participants` | `only_me`). Empty when the
  /// backend predates D-221, in which case [tierFor] derives the tier from the booleans instead.
  final Map<String, String> sections;

  /// The effective tier for one section, falling back to the legacy boolean that covers it.
  String tierFor(String section) {
    final stored = sections[section];
    if (stored != null && stored.isNotEmpty) return stored;
    return switch (section) {
      'profile' => profilePublic ? 'public' : 'only_me',
      'attended' => showAttended ? 'public' : 'only_me',
      'certificates' => showCertificates ? 'public' : 'only_me',
      'network' => showAllies ? 'public' : 'only_me',
      _ => 'public',
    };
  }
}
