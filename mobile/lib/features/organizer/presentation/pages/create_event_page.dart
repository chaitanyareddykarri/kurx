import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/kurx_button.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../events/domain/entities/event_category.dart';
import '../../../events/presentation/providers/search_providers.dart';
import '../../data/models/org_dto.dart';
import '../../domain/event_wizard_payload.dart';
import '../providers/event_content_providers.dart';
import '../providers/organizer_providers.dart';

/// Create an event — `POST /v1/events` (D-267).
///
/// Reachable directly: no organisation is chosen before this screen opens. **Representing** is the
/// wizard's first step and `Personal` is always offered, so a person can host under their own name
/// without registering anything. It used to be `POST /v1/orgs/{orgId}/events`, which meant the screen
/// could not exist until the caller had picked an organisation.
///
/// A wizard, matching the web one step for step so both surfaces teach the same flow (D-265).
/// This was a single form for a good reason at the time: `CreateEventBody` accepted only title,
/// category and dates, so there was genuinely nothing else to ask. D-265 added the content,
/// location, window, eligibility and legal fields, and the form had nowhere to put them.
///
/// The product flow names 18 steps. Nine of them — tickets, teams, modules, staff, sponsors, media,
/// preview, submit, finance — configure an event that must already exist, and each already has its
/// own manage screen. Duplicating them here would be a second implementation of each. This wizard's
/// job is a Draft complete enough to publish; the workspace finishes it.
///
/// The category list is the shared [categoriesProvider] — the same one Discover and the taxonomy
/// screens use, not a second fetch of `/v1/categories`.
class CreateEventPage extends ConsumerStatefulWidget {
  /// Chosen in the gate before this form opened (D-305). Selects which Types are offered — it is never
  /// sent to the server, which derives `Event.Product` from the Type itself (D-266 M1). Web passes the
  /// same value into `CreateEventWizard`.
  const CreateEventPage({super.key, this.product = 'Public', this.initialPricing = 'free'});

  final String product;

  /// Answered ONCE, in the gate (D-343): the product+pricing pair is what selected the verification
  /// tier the caller cleared to get here. This form READS it and offers no way to change it — the two
  /// guards were never the same guard (the gate also requires a verified representation, which the
  /// card here did not check), so a second control meant an event could become Paid after eligibility
  /// for Paid had been decided against it.
  final String initialPricing;

  @override
  ConsumerState<CreateEventPage> createState() => _CreateEventPageState();
}

/// The same eleven steps as web, in the same order (D-305). They were seven here — `basics` collapsed
/// visibility, category, type and details into one screen — which is how Flutter ended up silently
/// missing description, venue address, capacity, the two certificate/result dates, cancellation policy
/// and the whole pricing step. One product, one flow: a person who learns this on web must recognise it
/// here.
/// D-382 — `authorization` is gone as a step of its own. It sat LAST, after Legal, so an organiser
/// learned on step twelve that step one was incomplete; and it split one question — "who is this event
/// for, and who says so" — across two screens. It is asked on Representing now, with the organization
/// it authorises, exactly as web does.
enum _Step {
  representing, visibility, category, type, registration, details,
  content, location, windows, eligibility, legal
}

const _stepTitles = <_Step, String>{
  _Step.representing: 'Representing',
  _Step.visibility: 'Who can see it',
  _Step.category: 'Category',
  _Step.type: 'Type',
  _Step.registration: 'Registration',
  _Step.details: 'The basics',
  _Step.content: 'How it reads',
  _Step.location: 'Where it happens',
  _Step.windows: 'Key dates',
  _Step.eligibility: 'Who can join',
  _Step.legal: 'Terms',
};

class _CreateEventPageState extends ConsumerState<CreateEventPage> {
  // ── Representing ──────────────────────────────────────────────────────────
  /// The organisation this event is hosted on behalf of. Null until one is chosen — there is no
  /// "Personal" answer to fall back to (D-379), so this step is answered or the wizard does not advance.
  String? _representingOrgId;

  /// Organisations registered on THIS step, before `myRepresentationsProvider` has refetched. The
  /// staged organisation has to be selectable the instant it is created, or registering it inline would
  /// still leave the step unanswerable — which is the whole reason the redirect was removed.
  final List<RepresentationDto> _locallyAdded = [];

  /// The inline registration form — the fields the standalone request screen asks for, rendered in
  /// place. It used to be `context.push('/representing/new')`: a navigation out of a wizard holding ten
  /// steps of unsaved answers, so anyone without a representation lost the event they were creating.
  bool _orgFormOpen = false;
  final _orgName = TextEditingController();
  final _orgDomain = TextEditingController();
  String _orgType = 'college';
  List<int>? _orgProofBytes;
  String? _orgProofName;
  String _orgProofContentType = 'application/octet-stream';
  bool _orgSubmitting = false;
  String? _orgError;

  // ── Basics ────────────────────────────────────────────────────────────────
  final _title = TextEditingController();
  final _subtitle = TextEditingController();
  /// Required to PUBLISH (`ValidatePublishReadiness`). Flutter never sent it, so every draft this
  /// wizard created was unpublishable and the refusal only appeared at the publish button (D-305).
  final _description = TextEditingController();
  final _city = TextEditingController();
  final _venueName = TextEditingController();
  final _venueAddress = TextEditingController();
  final _capacity = TextEditingController();
  /// The gate's free/paid answer, carried in and never written again after [initState]. Nothing in this
  /// form may set it: the verification tier was chosen from it (D-343) before the form opened, so a
  /// control here would let an event become Paid after being judged as Free.
  String _pricing = 'free';
  String? _categoryId;
  String? _typeId;
  /// Unlisted is the only sane default for Private — Listed is forbidden and InviteOnly is a stronger
  /// claim than the organiser has made yet.
  late String _visibility;
  late DateTime _startsAt;
  late DateTime _endsAt;

  // ── The event's first ticket (D-305) ──────────────────────────────────────
  /// An event with NO ticket type cannot be registered for at all, so the wizard creates one. Rupees
  /// in, paise on the wire (D-004). Blank price = free.
  final _ticketName = TextEditingController(text: 'General Admission');
  final _ticketPrice = TextEditingController();
  final _ticketQuantity = TextEditingController(text: '100');

  /// D-372 — how people take part, which is what gives the price its unit. `team` maps to
  /// `RegistrationMode.Group` + `PricingUnit.PerGroup` together: one charge and one inventory unit for
  /// the whole team. Defaults to individual, which is what every event this wizard made before now was.
  String _participation = 'individual';
  final _teamMin = TextEditingController(text: '2');
  final _teamMax = TextEditingController(text: '4');

  /// D-366 — team-size price bands. Empty means one price for every size, which is D-372 unchanged and
  /// stays the default: an organiser who does not need bands never sees the table.
  final List<TeamPriceBand> _bands = [];

  // ── Content ───────────────────────────────────────────────────────────────
  final _tagline = TextEditingController();
  final _shortDescription = TextEditingController();
  final _rules = TextEditingController();

  // ── Location ──────────────────────────────────────────────────────────────
  String _eventMode = 'Offline';
  final _onlineUrl = TextEditingController();
  final _building = TextEditingController();
  final _floor = TextEditingController();
  final _room = TextEditingController();
  final _mapsUrl = TextEditingController();
  final _meetingPlatform = TextEditingController();
  final _meetingPassword = TextEditingController();

  // ── Windows ───────────────────────────────────────────────────────────────
  DateTime? _registrationOpensAt;
  DateTime? _registrationClosesAt;
  DateTime? _checkinOpensAt;
  DateTime? _checkinClosesAt;
  DateTime? _resultDate;
  DateTime? _certificateReleaseAt;
  bool _autoClose = false;

  // ── Eligibility ───────────────────────────────────────────────────────────
  final _minAge = TextEditingController();
  final _maxAge = TextEditingController();
  final _maxTeams = TextEditingController();
  String _gender = 'Any';

  // ── Legal ─────────────────────────────────────────────────────────────────
  final _termsUrl = TextEditingController();
  final _codeOfConduct = TextEditingController();
  final _refundPolicy = TextEditingController();
  final _cancellationPolicy = TextEditingController();
  bool _requiresConsent = false;
  final _consentText = TextEditingController();

  // ── Authorization (D-351) ─────────────────────────────────────────────────
  /// The institution's written consent, collected in-flow. The letter is held as bytes until the event
  /// exists, because both presign and submit are keyed on an eventId that only the create call
  /// produces — so abandoning the wizard uploads nothing.
  final _headName = TextEditingController();
  final _headDesignation = TextEditingController();
  final _officialEmail = TextEditingController();
  final _officialPhone = TextEditingController();
  String? _representativeRole;
  final _representativeRoleOther = TextEditingController();
  List<int>? _letterBytes;
  String? _letterName;
  String _letterContentType = 'application/octet-stream';

  /// D-379 — every event carries its own authorization, whatever its product. The letter proves "this
  /// representative may run THIS event for this organisation", which a private gathering needs as much
  /// as a public one, and `submit_review` refuses without it either way.
  ///
  /// D-382 — so there is nothing conditional left: every step is always present, and the letter is
  /// asked on Representing beside the organization it authorises.
  List<_Step> get _steps => _Step.values;

  // `_authorizationValid` lived here as a boolean that could only say "no". Replaced by
  // `validateEventAuthorization` in `_stepErrors`, which names the field — and adds the E.164 and
  // email shapes the boolean never checked, so a malformed phone no longer reaches the API as a 400
  // after the event has already been created.

  _Step _step = _Step.representing;
  bool _submitting = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _visibility = widget.product == 'Private' ? 'Unlisted' : 'Listed';
    // A Private product can never sell, so it pins Free regardless of what arrived on the route.
    _pricing = widget.product == 'Private' ? 'free' : widget.initialPricing;
    final now = DateTime.now();
    _startsAt = DateTime(now.year, now.month, now.day + 7, 10);
    _endsAt = _startsAt.add(const Duration(hours: 3));
  }

  @override
  void dispose() {
    for (final c in [
      _title, _subtitle, _description, _city, _venueName, _venueAddress, _capacity,
      _tagline, _shortDescription, _rules,
      _onlineUrl, _building, _floor, _room, _mapsUrl, _meetingPlatform, _meetingPassword,
      _minAge, _maxAge, _maxTeams,
      _termsUrl, _codeOfConduct, _refundPolicy, _cancellationPolicy, _consentText,
      _ticketName, _ticketPrice, _ticketQuantity, _teamMin, _teamMax,
      _orgName, _orgDomain,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  /// Does the chosen category actually OFFER a type for this product class?
  ///
  /// `_basicsValid` demanded `_typeId != null` unconditionally, but the Type step renders no dropdown
  /// at all when a category has no permitted types — it prints "No … types in this category yet." So on
  /// such a category `_typeId` could never become non-null and Continue was disabled forever, with
  /// nothing on screen to act on. Unreachable with today's taxonomy (every category has types) and
  /// reachable the moment an admin adds an empty one (D-188 makes the console the owner of that).
  ///
  /// Not loaded yet reads as "no types", i.e. the type is not demanded: the server takes `TypeId` as
  /// optional and resolves an absent one to Public, so failing open here matches both the server and
  /// web rather than blocking on a request that has not returned.
  bool get _categoryHasTypes {
    final types = ref.read(eventTypesProvider).valueOrNull;
    if (types == null || _categoryId == null) return false;
    return types.any((t) => t.parentId == _categoryId && t.allowsProduct(widget.product));
  }

  /// Rupees → paise. A blank price is free (0), never absent.
  int get _ticketPricePaise =>
      ((double.tryParse(_ticketPrice.text.trim()) ?? 0) * 100).round();

  /*
   * ── The wizard's one validation mechanism ────────────────────────────────────────────────────────
   *
   * `_Step → the step's rules → errors → Continue enabled/disabled`, for EVERY step, from ONE map.
   * This replaces two hand-written getters consulted at two step positions: `_basicsValid` on Details
   * and `_representingValid` on Representing/Pricing, with every other step ungated by design ("Every
   * other step stays ungated: blocking inside an optional step is how a wizard gets abandoned").
   *
   * That reasoning was right about optional steps and wrong about the steps that are not:
   *   · Location requires a join link once the event is Online (`online_url_required`);
   *   · Pricing's ticket must be nameable and countable, or the event is unbookable;
   *   · Legal requires the consent text once consent is switched on (`consent_text_required`);
   *   · Eligibility refuses an inverted age range (`invalid_age_range`);
   *   · Details asked nine questions and checked four.
   *
   * A step with genuinely no rules returns an empty map — an explicit statement that it is
   * all-optional, which is what the old comment was reaching for. Rebuilt from current state on every
   * `setState`, so nothing can hold a stale "valid".
   */
  Map<_Step, Map<String, String>> get _stepErrors => {
        // D-382 — both halves of the representation question, on the step that asks them.
        _Step.representing: {
          if (!_representingValid)
            'representingOrgId': 'Choose the organization you are hosting this event on behalf of',
          ...validateEventAuthorization(
            headName: _headName.text,
            headDesignation: _headDesignation.text,
            officialEmail: _officialEmail.text,
            officialPhone: _officialPhone.text,
            representativeRole: _representativeRole,
            representativeRoleOther: _representativeRoleOther.text,
            letterAttached: _letterBytes != null,
          ),
        },
        _Step.visibility: const {},
        // D-372 — the registration UNIT, asked after Type because the archetype decides whether team
        // entry exists at all. The paid-event eligibility check rides here too (D-365): it used to sit
        // on the deleted Pricing step, and this is the first step where money is actually typed.
        _Step.registration: {
          // Deliberately the paid-capable check, not `_representingValid`: a PendingReview organisation
          // may carry a free draft but may never back a paid event, and the server refuses that too.
          if (_pricing == 'paid' && !_representingPaidCapable)
            'representingOrgId':
                'Choose a verified organization — a paid event needs one Kurx has already verified',
          ...validateEventTicket(
            name: _ticketName.text,
            priceRupees: _ticketPrice.text,
            quantity: _ticketQuantity.text,
            paid: _pricing == 'paid',
            participation: _participation,
            teamMin: _teamMin.text,
            teamMax: _teamMax.text,
            bands: _bands,
          ),
        },
        _Step.category: _categoryId == null ? const {'categoryId': 'Choose a category to continue'} : const {},
        // Required only when the category HAS types — see `_categoryHasTypes`.
        _Step.type: (!_categoryHasTypes || _typeId != null)
            ? const {}
            : const {'typeId': 'Choose a type to continue'},
        _Step.details: validateEventDetails(
          title: _title.text,
          subtitle: _subtitle.text,
          description: _description.text,
          startsAt: _startsAt,
          endsAt: _endsAt,
          venueName: _venueName.text,
          city: _city.text,
          venueAddress: _venueAddress.text,
          capacity: _capacity.text,
        ),
        // D-378 — Content is required. The fields are still nullable on the wire (a draft saves
        // blank); this is the completeness rule that stops an unlistable event reaching review.
        _Step.content: validateEventContent(
          tagline: _tagline.text,
          shortDescription: _shortDescription.text,
          rules: _rules.text,
        ),
        // D-378 — every field the Mode asks for is required; the Mode decides which group applies.
        _Step.location: validateEventPlace(
          eventMode: _eventMode,
          onlineUrl: _onlineUrl.text,
          mapsUrl: _mapsUrl.text,
          building: _building.text,
          floor: _floor.text,
          room: _room.text,
          meetingPlatform: _meetingPlatform.text,
          meetingPassword: _meetingPassword.text,
        ),
        // `isPrivate` mirrors the step's rendering: a Private event is never shown the results or
        // certificate dates, so it must not be blocked on them.
        _Step.windows: validateEventWindows(
          registrationOpensAt: _registrationOpensAt,
          registrationClosesAt: _registrationClosesAt,
          checkinOpensAt: _checkinOpensAt,
          checkinClosesAt: _checkinClosesAt,
          resultDate: _resultDate,
          certificateReleaseAt: _certificateReleaseAt,
          isPrivate: widget.product == 'Private',
        ),
        _Step.eligibility: validateEventEligibility(
          minAge: _minAge.text,
          maxAge: _maxAge.text,
          maxTeams: _maxTeams.text,
          isPrivate: widget.product == 'Private',
        ),
        _Step.legal: validateEventLegal(
          termsUrl: _termsUrl.text,
          requiresConsent: _requiresConsent,
          consentText: _consentText.text,
          codeOfConduct: _codeOfConduct.text,
          refundPolicy: _refundPolicy.text,
          cancellationPolicy: _cancellationPolicy.text,
        ),
      };

  /// The current step's result — the one thing Continue is derived from.
  Map<String, String> get _currentErrors => _stepErrors[_step] ?? const {};

  /// What is still wrong across the whole wizard, for the final button and its list.
  List<String> get _missingForSubmit {
    final errors = _stepErrors;
    return [
      for (final step in _steps) ...errors[step]?.values ?? const <String>[],
    ];
  }

  /// May this representation carry the DRAFT? Any organisation the caller represents can, PendingReview
  /// included: `EventService.CreateAsync` refuses only a self-representation row or a deleted one, and
  /// `ResolveOrgAsync` grants Manager off the pending `Representative` seat. What a pending organisation
  /// cannot do is publish, which `pending_org_verification` blocks at transition time and this does not
  /// touch. Excluding pending ones here never enforced that rule — it only meant somebody who registered
  /// their college on this very step had nothing to select afterwards. Web's twin is `representingValid`.
  ///
  /// Fails CLOSED on a list that has not loaded: an absent answer means "we do not know this caller
  /// represents anything", and `_locallyAdded` is what carries an organisation registered inline before
  /// the provider has refetched.
  bool get _representingValid =>
      ref.read(myRepresentationsProvider).maybeWhen(
            data: (list) => [...list, ..._locallyAdded]
                .any((r) => r.organizationId == _representingOrgId),
            orElse: () => _locallyAdded.any((r) => r.organizationId == _representingOrgId),
          );

  /// D-350 — a PAID event needs a VERIFIED organisation: money settles into an institution's account,
  /// and the server refuses anything else at submit-for-review. Kept separate from `_representingValid`
  /// because "may draft" and "may charge" are different questions; collapsing them is what blocked the
  /// free path on a bar only the paid path has. Web's twin is `paidCapableReps`.
  bool get _representingPaidCapable =>
      ref.read(myRepresentationsProvider).maybeWhen(
            data: (list) => [...list, ..._locallyAdded].any((r) =>
                r.organizationId == _representingOrgId && (r.canBackPaidEvent || r.isVerified)),
            orElse: () => false,
          );

  bool get _canSubmit => _missingForSubmit.isEmpty && !_submitting;

  Future<void> _submit() async {
    if (!_canSubmit) return;
    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      final created = await ref.read(eventManageSourceProvider).createEvent(_representingOrgId, {
        'title': _title.text.trim(),
        'subtitle': _subtitle.text.trim().isEmpty ? null : _subtitle.text.trim(),
        'categoryId': _categoryId,
        'typeId': _typeId,
        'visibility': _visibility,
        'eventMode': _eventMode,
        'onlineUrl': _onlineUrl.text.trim().isEmpty ? null : _onlineUrl.text.trim(),
        'startsAt': _startsAt.toUtc().toIso8601String(),
        'endsAt': _endsAt.toUtc().toIso8601String(),
        'description': _description.text.trim().isEmpty ? null : _description.text.trim(),
        'venueName': _venueName.text.trim().isEmpty ? null : _venueName.text.trim(),
        'venueAddress': _venueAddress.text.trim().isEmpty ? null : _venueAddress.text.trim(),
        'city': _city.text.trim().isEmpty ? null : _city.text.trim(),
        'capacity': int.tryParse(_capacity.text.trim()),
        'content': compactGroup({
          'tagline': _tagline.text,
          'shortDescription': _shortDescription.text,
          'rules': _rules.text,
        }),
        'location': compactGroup({
          'building': _building.text,
          'floor': _floor.text,
          'room': _room.text,
          'googleMapsUrl': _mapsUrl.text,
          'meetingPlatform': _meetingPlatform.text,
          'meetingPassword': _meetingPassword.text,
        }),
        'schedule': compactGroup({
          'registrationOpensAt': _registrationOpensAt?.toUtc().toIso8601String(),
          'registrationClosesAt': _registrationClosesAt?.toUtc().toIso8601String(),
          'checkinOpensAt': _checkinOpensAt?.toUtc().toIso8601String(),
          'checkinClosesAt': _checkinClosesAt?.toUtc().toIso8601String(),
          'resultDate': _resultDate?.toUtc().toIso8601String(),
          'certificateReleaseAt': _certificateReleaseAt?.toUtc().toIso8601String(),
          'autoClose': _autoClose ? true : null,
        }),
        'eligibility': compactGroup({
          'minAge': int.tryParse(_minAge.text),
          'maxAge': int.tryParse(_maxAge.text),
          // "Any" is the server default; sending it would be noise.
          'genderRestriction': _gender == 'Any' ? null : _gender,
          'maxTeams': int.tryParse(_maxTeams.text),
        }),
        'legal': compactGroup({
          'termsUrl': _termsUrl.text,
          'codeOfConduct': _codeOfConduct.text,
          'refundPolicy': _refundPolicy.text,
          'cancellationPolicy': _cancellationPolicy.text,
          'requiresConsent': _requiresConsent ? true : null,
          'consentText': _consentText.text,
        }),
      });

      // The event's first ticket, created with it (D-305). An event with NO ticket type cannot be
      // registered for at all — the booking surface answers "no ticket types published yet" — so a
      // wizard that created only the event produced something nobody could join, and said nothing.
      //
      // Not fatal if it fails: the event is real, and losing seven steps of work to a ticket-shaped
      // error is worse than landing on the manage screen with the ticket still to add.
      String? ticketError;
      try {
        await ref.read(eventContentSourceProvider).createTicketType(created.orgId, created.id, {
          'name': _ticketName.text.trim().isEmpty ? 'General Admission' : _ticketName.text.trim(),
          // Rupees in, paise on the wire (D-004). Free is genuinely 0, never absent. With bands the
          // server derives the headline from the cheapest one, so this only carries the single-price
          // case (D-366).
          'pricePaise': _bands.isEmpty ? _ticketPricePaise : 0,
          /*
           * D-372 — the unit, no longer a literal.
           *
           * These two read `'PerTicket'` and `'Individual'` and made a capable API uni-modal: this app
           * could not create a team registration in any form, so an organiser on a phone could only
           * ever make an individual-entry event whatever the archetype allowed. `PerGroup` is what
           * tells the money path to charge once per team and take one inventory unit for it.
           */
          'pricingUnit': _participation == 'team' ? 'PerGroup' : 'PerTicket',
          'registrationMode': _participation == 'team' ? 'Group' : 'Individual',
          if (_participation == 'team') 'groupMin': int.tryParse(_teamMin.text.trim()),
          if (_participation == 'team') 'groupMax': int.tryParse(_teamMax.text.trim()),
          // D-366 — omitted entirely for an unbanded ticket, so that request stays what it always was.
          if (_participation == 'team' && _pricing == 'paid' && _bands.isNotEmpty)
            'priceTiers': teamPriceBandsPayload(_bands),
          'quantity': int.tryParse(_ticketQuantity.text.trim()) ?? 100,
          'saleStarts': DateTime.now().toUtc().toIso8601String(),
          // A ticket's window may narrow the event's later, never widen it, so equal is the only
          // starting value that cannot already be wrong.
          'saleEnds': _endsAt.toUtc().toIso8601String(),
          'perUserLimit': 1,
          'isAllAccess': false,
        });
      } on ApiError catch (e) {
        ticketError = e.userMessage;
      }

      // D-351 — file the institution's consent in the same action, so the organiser never leaves the
      // wizard to satisfy a publish blocker they were already asked about. It runs AFTER creation by
      // necessity: presign and submit are both keyed on an eventId only the create call produces.
      // That ordering means the event can exist while the authorization fails, so the failure is
      // reported rather than swallowed — the event is real and its consent is still missing.
      String? authError;
      try {
        final content = ref.read(eventContentSourceProvider);
        String? letterheadDocumentKey;
        if (_letterBytes != null) {
          final presign = await content.presignAuthorizationDoc(
              created.id, _letterContentType, _letterBytes!.length);
          await content.uploadToPresigned(presign, _letterBytes!, _letterContentType);
          letterheadDocumentKey = presign.key;
        }
        await content.submitAuthorization(created.id, {
          'headName': _headName.text.trim(),
          'headDesignation': _headDesignation.text.trim(),
          'officialEmail': _officialEmail.text.trim(),
          'officialPhone': _officialPhone.text.trim(),
          'representativeRole': _representativeRole ?? '',
          'representativeRoleOther':
              _representativeRoleOther.text.trim().isEmpty ? null : _representativeRoleOther.text.trim(),
          'letterheadDocumentKey': letterheadDocumentKey,
        });
      } on ApiError catch (e) {
        authError = e.userMessage;
      }

      ref.invalidate(myEventsProvider);
      if (!mounted) return;
      // Straight into the manage screen: a fresh Draft has no tickets and cannot be published, so
      // dropping the organiser back on a list would hide the next step.
      context.go('/events/${created.id}/manage');
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(authError != null
            ? 'Draft created, but its authorization was not filed: $authError'
            : ticketError == null
                ? 'Draft created with your ticket.'
                : 'Draft created, but the ticket failed: $ticketError')),
      );
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _error = e.userMessage;
      });
    }
  }

  /// Advances only if the current step re-validates, and says why if it does not.
  ///
  /// The disabled button is the affordance; this is the rule. They read the same `_stepErrors`, so
  /// they cannot disagree, and a tap that arrives from anywhere else still cannot skip a step.
  void _goNext(List<_Step> steps, int index) {
    final errors = _currentErrors;
    if (errors.isNotEmpty) {
      setState(() => _error = errors.values.first);
      return;
    }
    setState(() {
      _error = null;
      _step = steps[index + 1];
    });
  }

  /// The date/time picker.
  ///
  /// [floor] is the earliest selectable instant — `DateTime.now()` for the event's own start (the past
  /// is not offered at all), the chosen start for its end, and the opening time for a window's close.
  /// It replaces `firstDate: DateTime.now().subtract(const Duration(days: 1))`, which explicitly
  /// offered **yesterday**. Never a constant: the floor is computed from the clock or from the field it
  /// depends on, every time the picker opens.
  ///
  /// The date picker's `firstDate` is day-granular, so it alone cannot refuse an earlier time *today* —
  /// `validateEventDetails` is what holds that, and this narrows what has to be typed to reach it.
  Future<void> _pickDateTime({
    required DateTime? initial,
    required ValueChanged<DateTime> onPicked,
    DateTime? floor,
  }) async {
    final limit = floor ?? DateTime.now();
    var base = initial ?? DateTime.now().add(const Duration(days: 1));
    if (base.isBefore(limit)) base = limit;
    final date = await showDatePicker(
      context: context,
      initialDate: base,
      firstDate: DateTime(limit.year, limit.month, limit.day),
      lastDate: DateTime(base.year + 3),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(context: context, initialTime: TimeOfDay.fromDateTime(base));
    if (time == null) return;
    onPicked(DateTime(date.year, date.month, date.day, time.hour, time.minute));
  }

  @override
  Widget build(BuildContext context) {
    final steps = _steps;
    final index = steps.indexOf(_step);
    final isLast = index == steps.length - 1;

    return Scaffold(
      backgroundColor: context.kurx.background,
      appBar: AppBar(
        title: Text(_stepTitles[_step]!),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(4),
          child: LinearProgressIndicator(value: (index + 1) / steps.length, minHeight: 4),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(KSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Step ${index + 1} of ${steps.length}',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(color: context.kurx.muted),
            ),
            const SizedBox(height: KSpace.lg),
            switch (_step) {
              _Step.representing => _buildRepresenting(),
              _Step.visibility => _buildVisibility(),
              _Step.category => _buildCategory(),
              _Step.type => _buildType(),
              _Step.registration => _buildRegistration(),
              _Step.details => _buildDetails(),
              _Step.content => _buildContent(),
              _Step.location => _buildLocation(),
              _Step.windows => _buildWindows(),
              _Step.eligibility => _buildEligibility(),
              _Step.legal => _buildLegal(),
            },
            if (_error != null) ...[
              const SizedBox(height: KSpace.lg),
              Text(_error!, style: TextStyle(color: context.kurx.danger)),
            ],
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(KSpace.lg),
          child: Row(
            children: [
              if (index > 0) ...[
                Expanded(
                  child: KurxButton(
                    label: 'Back',
                    variant: KurxButtonVariant.secondary,
                    onPressed: _submitting ? null : () => setState(() => _step = steps[index - 1]),
                  ),
                ),
                const SizedBox(width: KSpace.md),
              ],
              Expanded(
                child: KurxButton(
                  label: isLast ? 'Create draft' : 'Continue',
                  loading: _submitting,
                  // Every step now gates its OWN fields, from `_stepErrors`. `onPressed: null` is
                  // presentation; `_goNext` re-reads the same result before advancing, so a
                  // programmatic tap or a stale rebuild cannot walk past an invalid step.
                  onPressed: isLast
                      ? (_canSubmit ? _submit : null)
                      : (_currentErrors.isEmpty ? () => _goNext(steps, index) : null),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  // ── Steps ─────────────────────────────────────────────────────────────────

  /// Who the event is hosted as, and the letter that says so. Mirrors web's Representing step.
  ///
  /// D-379 — EVERY event represents a real, verified organisation, whatever its product. There is no
  /// self-hosting card and no Personal branch: representing yourself is not a concept in this model
  /// (D-268), and visibility never decided who is answerable for an event.
  ///
  /// D-382 — the Private branch is gone, and it was not merely stale copy.
  ///
  /// It rendered "Hosted by you … there's no organisation to name and nothing to verify" and NO picker,
  /// while `_stepErrors[_Step.representing]` has demanded a valid representation for every product
  /// since D-379. So a Private event opened on a step with nothing to answer and a Continue button that
  /// could never enable: the Flutter wizard could not create one at all. Web retired the same branch;
  /// only this half was left behind.
  Widget _buildRepresenting() {
    final c = context.kurx;
    final reps = ref.watch(myRepresentationsProvider);
    return reps.maybeWhen(
      data: (list) {
        // Every representation is selectable, PendingReview included — a staged organisation can carry
        // a DRAFT (the server grants Manager off the pending `Representative` seat); what it cannot do
        // is publish, which `pending_org_verification` still blocks. Excluding them here never enforced
        // that rule and only stranded whoever had just registered one on this step.
        final selectable = [
          ...list,
          // Registered inline a moment ago; the provider has not refetched, and waiting for it would
          // put the step back in the state this change exists to remove.
          ..._locallyAdded.where((a) => !list.any((r) => r.organizationId == a.organizationId)),
        ];

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (selectable.isEmpty) ...[
              Container(
                padding: const EdgeInsets.all(KSpace.md),
                decoration: BoxDecoration(
                  border: Border.all(color: c.border, style: BorderStyle.solid),
                  borderRadius: BorderRadius.circular(KRadius.md),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text("You don't represent an organisation yet.",
                        style: TextStyle(color: c.text, fontSize: 14)),
                    const SizedBox(height: KSpace.xs),
                    Text(
                      'Every event is hosted on behalf of an organisation. Add it below — you can carry '
                      'on creating this event straight away; an admin verifies the organisation before '
                      'the event can be published.',
                      style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
                    ),
                  ],
                ),
              ),
            ] else ...[
              _label('Who are you hosting this event on behalf of?'),
              Text('Select the organisation you are authorised to represent for this event.',
                  style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35)),
              const SizedBox(height: KSpace.sm),
              RadioGroup<String?>(
                groupValue: _representingOrgId,
                onChanged: (v) => setState(() => _representingOrgId = v),
                child: Column(
                  children: [
                    for (final r in selectable)
                      RadioListTile<String?>(
                        value: r.organizationId,
                        title: Text(r.name),
                        // A pending organisation says so on its own row rather than being listed
                        // separately as unusable: it CAN carry this draft, and what it cannot do is
                        // publish — which is what this now states.
                        subtitle: Text(
                          r.canBackPaidEvent || r.isVerified
                              ? 'Representing · your authority: ${r.authority}'
                              : 'Awaiting admin verification · you can start the event now, but it '
                                  "can't publish until that's approved",
                        ),
                      ),
                  ],
                ),
              ),
            ],
            const SizedBox(height: KSpace.md),
            // The registration form, RENDERED HERE rather than pushed to. It was
            // `context.push('/representing/new')`: a navigation out of a wizard holding ten steps of
            // unsaved answers, so anyone without a representation lost the event they were creating.
            _buildOrgRegistration(),
            // D-382 — the letter, on the same step as the organization it authorises, and only once
            // one is actually chosen: it names that organization, so asking for it first is asking
            // about nothing. Was a twelfth step after Legal.
            if (_representingValid) ...[
              const SizedBox(height: KSpace.xl),
              const Divider(),
              const SizedBox(height: KSpace.lg),
              _buildAuthorization(),
            ],
          ],
        );
      },
      orElse: () => const Center(child: CircularProgressIndicator()),
    );
  }

  /// Register a college or organisation **without leaving this step**.
  ///
  /// Field for field the standalone request screen (`RequestRepresentationPage`) and the same two API
  /// calls — presign the proof, then `POST /v1/orgs/representation-requests`. What differs is only what
  /// happens next: nothing is navigated, the staged organisation is appended to the picker and
  /// selected, and the wizard carries on holding every answer given so far.
  Widget _buildOrgRegistration() {
    final c = context.kurx;
    final busy = _orgSubmitting || _submitting;

    if (!_orgFormOpen) {
      return Align(
        alignment: Alignment.centerLeft,
        child: TextButton(
          onPressed: busy ? null : () => setState(() => _orgFormOpen = true),
          child: const Text('Add your college or organisation'),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        border: Border.all(color: c.border),
        borderRadius: BorderRadius.circular(KRadius.md),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Add your college or organisation',
              style: TextStyle(color: c.text, fontSize: 14, fontWeight: FontWeight.w600)),
          const SizedBox(height: KSpace.xs),
          Text(
            'An admin verifies the institution itself before this event can publish — a one-time step '
            "per organisation. This event's own authorisation letter is asked for below and is needed "
            'for every event, however many you run under the same organisation.',
            style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
          ),
          const SizedBox(height: KSpace.lg),
          TextField(
            controller: _orgName,
            enabled: !busy,
            onChanged: (_) => setState(() {}),
            decoration: const InputDecoration(
              labelText: 'Organisation name',
              hintText: 'e.g. NSRIT College',
            ),
          ),
          const SizedBox(height: KSpace.md),
          DropdownButtonFormField<String>(
            initialValue: _orgType,
            decoration: const InputDecoration(labelText: 'Type'),
            items: [for (final t in kOrgTypes) DropdownMenuItem(value: t, child: Text(t))],
            onChanged: busy ? null : (v) => setState(() => _orgType = v ?? 'college'),
          ),
          const SizedBox(height: KSpace.md),
          TextField(
            controller: _orgDomain,
            enabled: !busy,
            decoration: const InputDecoration(
              labelText: 'Organisation email domain (optional)',
              hintText: 'nsrit.edu.in',
            ),
          ),
          const SizedBox(height: KSpace.lg),
          Text('Proof of affiliation',
              style: TextStyle(color: c.text, fontSize: 13, fontWeight: FontWeight.w600)),
          const SizedBox(height: KSpace.xs),
          Text(
            'A letterhead, official document or authorisation proof showing you represent this '
            'organisation. PDF or image.',
            style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
          ),
          const SizedBox(height: KSpace.sm),
          KurxButton(
            label: _orgProofName == null ? 'Choose file' : 'Replace file',
            variant: KurxButtonVariant.secondary,
            onPressed: busy ? null : _pickOrgProof,
          ),
          if (_orgProofName != null) ...[
            const SizedBox(height: KSpace.xs),
            Text(_orgProofName!, style: TextStyle(color: c.muted, fontSize: 12.5)),
          ],
          if (_orgError != null) ...[
            const SizedBox(height: KSpace.md),
            Text(_orgError!, style: TextStyle(color: c.danger, fontSize: 12.5)),
          ],
          const SizedBox(height: KSpace.lg),
          KurxButton(
            label: 'Save organisation',
            loading: _orgSubmitting,
            onPressed: busy ? null : _submitOrgRegistration,
          ),
        ],
      ),
    );
  }

  static const _proofTypes = ['pdf', 'jpg', 'jpeg', 'png'];

  Future<void> _pickOrgProof() async {
    final picked = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: _proofTypes,
      withData: true,
    );
    final file = picked?.files.singleOrNull;
    final bytes = file?.bytes;
    if (file == null || bytes == null) return;

    // FilePicker's extension filter is advisory on some platforms, so it is re-checked rather than
    // trusted — the same discipline `_pickLetter` applies to the authorisation letter.
    final ext = file.extension?.toLowerCase();
    if (!_proofTypes.contains(ext)) {
      setState(() => _orgError = 'Attach a PDF or an image.');
      return;
    }
    setState(() {
      _orgProofBytes = bytes;
      _orgProofName = file.name;
      _orgProofContentType = ext == 'pdf' ? 'application/pdf' : 'image/${ext == 'jpg' ? 'jpeg' : ext}';
      _orgError = null;
    });
  }

  Future<void> _submitOrgRegistration() async {
    // Both are what the server requires: a name, and evidence. A request with no proof is a claim.
    if (_orgName.text.trim().isEmpty) {
      setState(() => _orgError = 'Enter the organisation name');
      return;
    }
    if (_orgProofBytes == null) {
      setState(() => _orgError = 'Attach proof of affiliation');
      return;
    }
    setState(() {
      _orgSubmitting = true;
      _orgError = null;
    });

    try {
      final orgs = ref.read(orgSourceProvider);
      // The bytes go straight to storage; only the key it returns is submitted.
      final presign = await orgs.presignRepresentationDoc(_orgProofContentType, _orgProofBytes!.length);
      await ref
          .read(eventContentSourceProvider)
          .uploadToPresigned(presign, _orgProofBytes!, _orgProofContentType);
      final org = await orgs.submitRepresentationRequest({
        'name': _orgName.text.trim(),
        'type': _orgType,
        'primaryDomain': _orgDomain.text.trim().isEmpty ? null : _orgDomain.text.trim(),
        'documents': [
          {'docType': 'letterhead', 'storageKey': presign.key},
        ],
      });
      // Refetch for the authoritative row, AND hold a local copy: the provider is async and the step
      // has to be answerable the moment the organisation exists. A freshly staged org is PendingReview
      // by construction — draftable, never paid-capable — so both flags are false.
      ref.invalidate(myRepresentationsProvider);
      if (!mounted) return;
      setState(() {
        _locallyAdded.add(RepresentationDto(
          organizationId: org.id,
          name: org.name,
          slug: org.slug,
          logoKey: org.logoKey,
          // `OrgDto.role` is nullable on the list shape; the representation-request response always
          // carries it, and a staged request makes the caller a pending Representative by construction.
          authority: org.role ?? 'representative',
        ));
        _representingOrgId = org.id;
        _orgFormOpen = false;
        _orgSubmitting = false;
        _orgName.clear();
        _orgDomain.clear();
        _orgProofBytes = null;
        _orgProofName = null;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Organisation added. An admin verifies it before you can publish.')),
      );
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _orgError = e.userMessage;
        _orgSubmitting = false;
      });
    }
  }

  static const _letterTypes = ['pdf', 'jpg', 'jpeg', 'png'];

  Future<void> _pickLetter() async {
    final picked = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: _letterTypes,
      withData: true,
    );
    final file = picked?.files.singleOrNull;
    final bytes = file?.bytes;
    if (file == null || bytes == null) return;

    // FilePicker's extension filter is advisory on some platforms, so the check is repeated here
    // rather than trusted.
    final ext = file.extension?.toLowerCase();
    if (!_letterTypes.contains(ext)) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Attach a PDF or an image.')),
        );
      }
      return;
    }
    setState(() {
      _letterBytes = bytes;
      _letterName = file.name;
      _letterContentType = ext == 'pdf' ? 'application/pdf' : 'image/${ext == 'jpg' ? 'jpeg' : ext}';
    });
  }

  /// D-351 — the institution's written consent, mirroring web's Authorization step field for field.
  Widget _buildAuthorization() {
    final c = context.kurx;
    final orgName = ref.watch(myRepresentationsProvider).maybeWhen(
          data: (list) => list
              .where((r) => r.organizationId == _representingOrgId)
              .map((r) => r.name)
              .firstOrNull,
          orElse: () => null,
        );
    final roles = ref.watch(representativeRolesProvider);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          '${orgName ?? 'This organisation'} has to confirm it authorises this event. Filed once, with '
          "the event — a reviewer reads it as part of the event's review, and the event can't publish "
          'until it is approved.',
          style: TextStyle(color: c.muted, fontSize: 13, height: 1.35),
        ),
        const SizedBox(height: KSpace.lg),
        _field(_headName, "Signatory's name"),
        _field(_headDesignation, 'Their designation'),
        _field(_officialEmail, 'Official email', keyboard: TextInputType.emailAddress),
        _field(_officialPhone, 'Official phone',
            hint: '+919876543210', keyboard: TextInputType.phone),
        const SizedBox(height: KSpace.md),
        _label('Your role in this organisation'),
        // The server's vocabulary, never a copy — a list that drifts offers a role the API refuses.
        roles.maybeWhen(
          data: (list) => DropdownButtonFormField<String>(
            initialValue: _representativeRole,
            items: [
              for (final r in list) DropdownMenuItem(value: r, child: Text(r)),
            ],
            onChanged: (v) => setState(() => _representativeRole = v),
          ),
          orElse: () => const LinearProgressIndicator(),
        ),
        if (_representativeRole == 'Other') ...[
          const SizedBox(height: KSpace.md),
          _field(_representativeRoleOther, 'Describe your role'),
        ],
        const SizedBox(height: KSpace.lg),
        _label('Authorization letter'),
        const SizedBox(height: KSpace.xs),
        // The letter is per-EVENT, not per-organisation: `event_authorizations` is UNIQUE on EventId, so
        // representing the same organisation again next month needs a new letter naming that event.
        // Web states the same four requirements, in the same words.
        Container(
          padding: const EdgeInsets.all(KSpace.md),
          decoration: BoxDecoration(
            border: Border.all(color: c.border),
            borderRadius: BorderRadius.circular(KRadius.md),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('The letter must be specific to this event and state:',
                  style: TextStyle(color: c.text, fontSize: 12.5)),
              const SizedBox(height: KSpace.xs),
              _LetterPoint('the event by name — '
                  '${_title.text.trim().isEmpty ? 'your event title' : _title.text.trim()}'),
              _LetterPoint('its dates — ${DateFormat('d MMM y, h:mm a').format(_startsAt)} '
                  'to ${DateFormat('d MMM y, h:mm a').format(_endsAt)}'),
              const _LetterPoint(
                  "that you are authorised to organise it on the organisation's behalf"),
              const _LetterPoint("the signatory's name, designation and signature"),
              const SizedBox(height: KSpace.sm),
              Text(
                "On the organisation's official letterhead. PDF or image, up to 10 MB. A reviewer reads it "
                "as part of this event's review — a generic authorisation letter that does not name the "
                'event is usually rejected.',
                style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
              ),
            ],
          ),
        ),
        const SizedBox(height: KSpace.sm),
        KurxButton(
          label: _letterName == null ? 'Choose file' : 'Replace file',
          variant: KurxButtonVariant.secondary,
          onPressed: _submitting ? null : _pickLetter,
        ),
        if (_letterName != null) ...[
          const SizedBox(height: KSpace.xs),
          Text(_letterName!, style: TextStyle(color: c.text, fontSize: 12.5)),
        ],
      ],
    );
  }


  Widget _buildVisibility() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _label('Who can see it'),
          // The three values `EventVisibility` actually has. A fourth, `Private`, was offered here and
          // had been dead since the `RetireLegacyPrivateVisibility` migration: the server's parse
          // rejects it, so an organiser choosing it silently got **Listed** — a privacy failure that
          // looked like it worked. Web removed it in D-266 M8; this is the same fix on mobile.
          // D-307 — a Private product can NEVER be Listed: `PolicyResolver` answers
          // `private_product_cannot_be_listed`, so offering it would accept an input the server refuses
          // eleven steps later. Filtered here, exactly as web's `visibilityFor` does.
          DropdownButtonFormField<String>(
            initialValue: _visibility,
            isExpanded: true,
            decoration: const InputDecoration(border: OutlineInputBorder()),
            items: [
              if (widget.product != 'Private')
                const DropdownMenuItem(value: 'Listed', child: Text('Listed — shown in discovery')),
              const DropdownMenuItem(value: 'Unlisted', child: Text('Unlisted — link only')),
              const DropdownMenuItem(value: 'InviteOnly', child: Text('Invite only')),
            ],
            onChanged: (v) => setState(() => _visibility = v ?? 'Unlisted'),
          ),
        ],
      );

  /// D-372/D-366 — the registration option: what people book, HOW they take part, what that costs in
  /// the unit it is charged in, and how many of that unit exist.
  ///
  /// Placed after Type because the Type carries the archetype, and the archetype's `teams` capability
  /// is the only thing that may decide whether team entry is offered (D-266 M2). Asking earlier — which
  /// is where the ticket fields used to sit — meant the unit could only ever be a guess.
  Widget _buildRegistration() {
    final c = context.kurx;
    final types = ref.watch(eventTypesProvider).valueOrNull ?? const <EventCategory>[];
    final archetype = types.where((t) => t.id == _typeId).firstOrNull?.archetypeSlug;
    final teamsSupported = ref.watch(archetypeSupportsTeamsProvider(archetype)).valueOrNull ?? false;

    // Reconciled, never left invalid — web does the same in a `useEffect`. Going back and changing the
    // Type to one whose archetype has no `teams` capability HIDES the radio, and without this the
    // answer would survive underneath it: a team ticket submitted for a conference, which the server
    // accepts because the capability engine describes rather than enforces (D-266 M2). Bands go with
    // it — they mean nothing on an individual ticket and would be sent to a refusal.
    if (!teamsSupported && _participation == 'team') {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        setState(() {
          _participation = 'individual';
          _bands.clear();
        });
      });
    }

    final team = _participation == 'team' && teamsSupported;
    final paid = _pricing == 'paid';
    final errors = _currentErrors;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // States the pricing mode; never asks it (D-365). The gate settled it, and this is the step
        // where its consequence appears — a price field, or the absence of one.
        Text(
          paid
              ? 'Paid event — chosen during setup. Set what people pay below.'
              : 'Free event — chosen during setup. No one will be charged to register.',
          style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
        ),
        const SizedBox(height: KSpace.md),
        _label('What people are booking'),
        _field(_ticketName, 'Registration name'),
        if (errors['name'] != null) _fieldError(errors['name']!),

        // Individual vs team, offered only where the archetype allows it: a conference marks `teams`
        // Unsupported, and offering it there would configure something the event can never run.
        if (teamsSupported) ...[
          const SizedBox(height: KSpace.md),
          _label('How do people take part?'),
          RadioGroup<String>(
            groupValue: _participation,
            onChanged: (v) => setState(() => _participation = v ?? 'individual'),
            child: const Column(
              children: [
                RadioListTile<String>(
                  value: 'individual',
                  title: Text('Individually'),
                  subtitle: Text('Each person registers for themselves.'),
                ),
                RadioListTile<String>(
                  value: 'team',
                  title: Text('As a team'),
                  subtitle: Text('One person registers the team and the rest join it.'),
                ),
              ],
            ),
          ),
        ] else
          Padding(
            padding: const EdgeInsets.only(top: KSpace.sm),
            child: Text(
              "This kind of event doesn't support team entry, so people register individually.",
              style: TextStyle(color: c.muted, fontSize: 12.5),
            ),
          ),

        if (team) ...[
          const SizedBox(height: KSpace.md),
          _label('Team size'),
          Row(
            children: [
              Expanded(child: _field(_teamMin, 'Smallest team', keyboard: TextInputType.number)),
              const SizedBox(width: KSpace.md),
              Expanded(child: _field(_teamMax, 'Largest team', keyboard: TextInputType.number)),
            ],
          ),
          if (errors['teamMin'] != null) _fieldError(errors['teamMin']!),
          if (errors['teamMax'] != null) _fieldError(errors['teamMax']!),
        ],

        // D-366 — price by team size. Only for a PAID TEAM ticket: an individual price already scales
        // with the roster, and a free event has no prices to band.
        if (paid && team) ...[
          const SizedBox(height: KSpace.lg),
          Row(
            children: [
              Expanded(child: _label('Price by team size')),
              if (_bands.isEmpty)
                TextButton(
                  onPressed: () => setState(() => _bands.add(TeamPriceBand(
                        // Seeded across the whole allowed range so the first thing shown is already a
                        // valid set — an editor that opens invalid teaches people to ignore it.
                        minSize: _teamMin.text.trim(),
                        maxSize: _teamMax.text.trim(),
                        priceRupees: _ticketPrice.text.trim(),
                      ))),
                  child: const Text('Different prices per size'),
                )
              else
                TextButton(
                  onPressed: () => setState(_bands.clear),
                  child: const Text('One price for all'),
                ),
            ],
          ),
          if (_bands.isEmpty)
            Text(
              'Every team pays the same, whatever its size. Add rules to charge a team of 2 differently '
              'from a team of 5.',
              style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
            )
          else ...[
            for (var i = 0; i < _bands.length; i++)
              Padding(
                padding: const EdgeInsets.only(bottom: KSpace.sm),
                child: Row(
                  children: [
                    Expanded(
                      child: _bandField(
                        initial: _bands[i].minSize,
                        label: 'From',
                        onChanged: (v) => setState(() => _bands[i].minSize = v),
                      ),
                    ),
                    const SizedBox(width: KSpace.sm),
                    Expanded(
                      child: _bandField(
                        initial: _bands[i].maxSize,
                        label: 'To',
                        onChanged: (v) => setState(() => _bands[i].maxSize = v),
                      ),
                    ),
                    const SizedBox(width: KSpace.sm),
                    Expanded(
                      flex: 2,
                      child: _bandField(
                        initial: _bands[i].priceRupees,
                        label: 'Price / team (Rs.)',
                        onChanged: (v) => setState(() => _bands[i].priceRupees = v),
                      ),
                    ),
                    IconButton(
                      tooltip: 'Remove rule',
                      icon: const Icon(Icons.close_rounded, size: 18),
                      onPressed: () => setState(() => _bands.removeAt(i)),
                    ),
                  ],
                ),
              ),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                icon: const Icon(Icons.add_rounded, size: 18),
                label: const Text('Add price rule'),
                onPressed: () => setState(() {
                  // The next rule starts where the last one ended: a set built by hand is where gaps
                  // come from, and the common case is contiguous bands.
                  final last = _bands.isEmpty ? null : _bands.last;
                  final next = ((int.tryParse(last?.maxSize.trim() ?? '') ?? 1) + 1).toString();
                  _bands.add(TeamPriceBand(minSize: next, maxSize: next));
                }),
              ),
            ),
            if (errors['bands'] != null)
              _fieldError(errors['bands']!)
            else
              Text(
                'Each rule is the price for the WHOLE team, not per member. Every allowed team size '
                'needs exactly one rule.',
                style: TextStyle(color: c.muted, fontSize: 12, height: 1.35),
              ),
          ],
        ],

        const SizedBox(height: KSpace.lg),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Hidden once bands exist: the bands ARE the price then, and two price inputs for one
            // decision is the duplicate-question mistake again (D-365).
            if (paid && _bands.isEmpty) ...[
              Expanded(
                child: _field(_ticketPrice,
                    team ? 'Price per team (Rs.)' : 'Price per participant (Rs.)',
                    keyboard: TextInputType.number),
              ),
              const SizedBox(width: KSpace.md),
            ],
            // Under PerGroup one team takes exactly one unit, so for a team event this counts TEAMS.
            Expanded(
              child: _field(_ticketQuantity, team ? 'How many teams' : 'How many places',
                  keyboard: TextInputType.number),
            ),
          ],
        ),
        if (errors['priceRupees'] != null) _fieldError(errors['priceRupees']!),
        if (errors['quantity'] != null) _fieldError(errors['quantity']!),

        const SizedBox(height: KSpace.md),
        Text(_registrationSummary(), style: TextStyle(color: c.muted, fontSize: 12, height: 1.35)),
      ],
    );
  }

  /// Reads back what was configured, in the unit it is charged in — a price with no unit is the
  /// ambiguity this step exists to remove. With bands it names the RANGE, because one number on a
  /// ticket that also charges another would be the same lie.
  String _registrationSummary() {
    final team = _participation == 'team';
    final qty = _ticketQuantity.text.trim().isEmpty ? '—' : _ticketQuantity.text.trim();
    final sizes = '${_teamMin.text.trim()}–${_teamMax.text.trim()}';
    if (_pricing != 'paid') {
      return team ? 'Free · teams of $sizes · $qty team slots' : 'Free · $qty places';
    }
    if (team && _bands.isNotEmpty) {
      final prices = _bands.map((b) => double.tryParse(b.priceRupees.trim()) ?? 0).toList()..sort();
      return 'Rs.${prices.first.round()}–Rs.${prices.last.round()} per team by size · '
          'teams of $sizes · $qty team slots';
    }
    final price = _ticketPrice.text.trim().isEmpty ? '—' : _ticketPrice.text.trim();
    return team
        ? 'Rs.$price per team · teams of $sizes · $qty team slots'
        : 'Rs.$price per participant · $qty places';
  }

  /// A band cell. `initialValue` rather than a controller per cell: bands are added and removed, and a
  /// controller list has to be kept in lockstep with the model or it feeds the wrong row's text back.
  Widget _bandField({
    required String initial,
    required String label,
    required ValueChanged<String> onChanged,
  }) =>
      TextFormField(
        initialValue: initial,
        keyboardType: TextInputType.number,
        decoration: InputDecoration(labelText: label, border: const OutlineInputBorder(), isDense: true),
        onChanged: onChanged,
      );

  Widget _fieldError(String message) => Padding(
        padding: const EdgeInsets.only(top: 4),
        child: Text(message, style: TextStyle(color: context.kurx.danger, fontSize: 12)),
      );

  Widget _buildCategory() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _label('Category'),
          AsyncValueView(
            value: ref.watch(categoriesProvider),
            onRetry: () => ref.invalidate(categoriesProvider),
            data: (List<EventCategory> list) => DropdownButtonFormField<String>(
              initialValue: _categoryId,
              isExpanded: true,
              decoration: const InputDecoration(border: OutlineInputBorder()),
              hint: const Text('Pick a category'),
              items: [
                for (final c in list)
                  DropdownMenuItem(value: c.id, child: Text(c.name, overflow: TextOverflow.ellipsis)),
              ],
              onChanged: (v) => setState(() {
                _categoryId = v;
                _typeId = null; // a type belongs to one category; keeping it would post a mismatched pair
              }),
            ),
          ),
        ],
      );

  /// The TYPE carries the archetype, which decides what the event supports and which policy rules apply.
  /// Without it an event resolves to no archetype at all and every capability reads Unsupported.
  Widget _buildType() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _label('Type'),
          if (_categoryId == null)
            const Text('Pick a category first.')
          else
            AsyncValueView(
              value: ref.watch(eventTypesProvider),
              onRetry: () => ref.invalidate(eventTypesProvider),
              data: (List<EventCategory> all) {
                // Only Types the chosen product class permits. `allowsProduct` lives on the entity so
                // web and Flutter apply one predicate — web's `typesFor()` is the same rule (D-305).
                final types = all
                    .where((t) => t.parentId == _categoryId && t.allowsProduct(widget.product))
                    .toList();
                if (types.isEmpty) {
                  return Text('No ${widget.product.toLowerCase()} types in this category yet.');
                }
                return DropdownButtonFormField<String>(
                  initialValue: _typeId,
                  isExpanded: true,
                  decoration: const InputDecoration(border: OutlineInputBorder()),
                  hint: const Text('Pick a type'),
                  items: [
                    for (final t in types)
                      DropdownMenuItem(value: t.id, child: Text(t.name, overflow: TextOverflow.ellipsis)),
                  ],
                  onChanged: (v) => setState(() => _typeId = v),
                );
              },
            ),
        ],
      );

  /// Every field here is required by the STEP, and the errors come from `validateEventDetails` — the
  /// same result Continue is derived from, so a field can never disagree with the button.
  ///
  /// Six of these were previously unchecked (`_basicsValid` read title, category, type and the date
  /// ordering only), and "Subtitle · One line that sells it (optional)" said so on screen.
  Widget _buildDetails() {
    final errors = _currentErrors;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _field(_title, 'Title', hint: 'What is your event called?', error: errors['title']),
        _field(_subtitle, 'Subtitle', hint: 'One line that sells it', error: errors['subtitle']),
        _field(_description, 'Description', lines: 4,
            hint: 'Required before you can publish', error: errors['description']),
        _label('When'),
        // The start cannot be picked in the past at all; moving it drags an end that no longer
        // follows it, which is the "changing Start invalidates End" case.
        _dateTile('Starts', _startsAt, (d) => setState(() {
              _startsAt = d;
              if (!_endsAt.isAfter(_startsAt)) _endsAt = _startsAt.add(const Duration(hours: 3));
            })),
        // The end's floor is the start, so the picker cannot offer a day before it.
        _dateTile('Ends', _endsAt, (d) => setState(() => _endsAt = d), floor: _startsAt),
        for (final key in const ['startsAt', 'endsAt'])
          if (errors[key] != null)
            Padding(
              padding: const EdgeInsets.only(top: KSpace.sm, bottom: KSpace.sm),
              child: Text(errors[key]!,
                  style: TextStyle(color: context.kurx.danger, fontSize: 13)),
            ),
        _field(_venueName, 'Venue name', error: errors['venueName']),
        _field(_city, 'City', error: errors['city']),
        _field(_venueAddress, 'Venue address', error: errors['venueAddress']),
        _field(_capacity, 'Capacity', keyboard: TextInputType.number, error: errors['capacity']),
      ],
    );
  }

  /// D-378 — required, and errors wired the same way Details wires its own: from `_currentErrors`,
  /// which is the map Continue is derived from, so a field can never disagree with the button.
  Widget _buildContent() {
    final errors = _currentErrors;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _hint('This is what a listing card and a shared link show.'),
        _field(_tagline, 'Tagline', maxLength: 160, error: errors['tagline']),
        _field(_shortDescription, 'Short description',
            maxLength: 300, lines: 2, error: errors['shortDescription']),
        _field(_rules, 'Rules', lines: 4, error: errors['rules']),
      ],
    );
  }

  Widget _buildLocation() {
    final errors = _currentErrors;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _label('Mode'),
        DropdownButtonFormField<String>(
          initialValue: _eventMode,
          isExpanded: true,
          decoration: const InputDecoration(border: OutlineInputBorder()),
          items: const [
            DropdownMenuItem(value: 'Offline', child: Text('In person')),
            DropdownMenuItem(value: 'Online', child: Text('Online')),
            DropdownMenuItem(value: 'Hybrid', child: Text('Hybrid')),
          ],
          // Changing Mode is what makes the join link required or optional — the step's validity is
          // recomputed on this `setState`, never carried over from before the change.
          onChanged: (v) => setState(() => _eventMode = v ?? 'Offline'),
        ),
        const SizedBox(height: KSpace.md),
        if (_eventMode != 'Online') ...[
          _field(_venueName, 'Venue name'),
          _field(_city, 'City'),
          Row(
            children: [
              Expanded(child: _field(_building, 'Building')),
              const SizedBox(width: KSpace.md),
              Expanded(child: _field(_floor, 'Floor')),
              const SizedBox(width: KSpace.md),
              Expanded(child: _field(_room, 'Room')),
            ],
          ),
          _field(_mapsUrl, 'Google Maps link',
              keyboard: TextInputType.url, error: errors['mapsUrl']),
        ],
        if (_eventMode != 'Offline') ...[
          // Required by `ValidateMode` for exactly these two modes (`online_url_required`).
          _field(_onlineUrl, 'Join link',
              keyboard: TextInputType.url, error: errors['onlineUrl']),
          _field(_meetingPlatform, 'Platform', hint: 'Zoom, Meet, Teams…'),
          _field(_meetingPassword, 'Meeting password'),
          _hint('The password is only shown to confirmed registrants.'),
        ],
      ],
    );
  }

  Widget _buildWindows() {
    final errors = _currentErrors;
    // Each close is floored at its own open, so the picker cannot offer the pair inverted — the same
    // rule `invalid_registration_window` / `invalid_checkin_window` refuse.
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _hint("All optional. Registration times bound every ticket type; a ticket's own sale "
            'window can narrow that further, never widen it.'),
        _label('Registration'),
        _dateTile('Opens', _registrationOpensAt, (d) => setState(() => _registrationOpensAt = d),
            onClear: () => setState(() => _registrationOpensAt = null)),
        _dateTile('Closes', _registrationClosesAt, (d) => setState(() => _registrationClosesAt = d),
            onClear: () => setState(() => _registrationClosesAt = null),
            floor: _registrationOpensAt),
        if (errors['registrationClosesAt'] != null) _inlineError(errors['registrationClosesAt']!),
        const SizedBox(height: KSpace.md),
        _label('Check-in'),
        _dateTile('Opens', _checkinOpensAt, (d) => setState(() => _checkinOpensAt = d),
            onClear: () => setState(() => _checkinOpensAt = null)),
        _dateTile('Closes', _checkinClosesAt, (d) => setState(() => _checkinClosesAt = d),
            onClear: () => setState(() => _checkinClosesAt = null), floor: _checkinOpensAt),
        if (errors['checkinClosesAt'] != null) _inlineError(errors['checkinClosesAt']!),
        const SizedBox(height: KSpace.md),
          // Both were in `EventScheduleInput` and on web's Windows step from the start; Flutter simply
          // never sent them, so a competition's result date and a certificate release could not be set
          // at creation on mobile (D-305).
          // D-327 — `scoring` and `certificates` are Unsupported for the private-gathering archetype,
          // so a wedding was asked when its results are announced. Same predicate as web's Windows
          // step; Private ⇔ private-gathering is a one-to-one the D12 matrix asserts.
          if (widget.product != 'Private') ...[
            _label('Results & certificates'),
            _dateTile('Results announced', _resultDate, (d) => setState(() => _resultDate = d),
                onClear: () => setState(() => _resultDate = null)),
            _dateTile('Certificates released', _certificateReleaseAt,
                (d) => setState(() => _certificateReleaseAt = d),
                onClear: () => setState(() => _certificateReleaseAt = null)),
          ],
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          value: _autoClose,
          title: const Text('Close registration when capacity is reached'),
          onChanged: (v) => setState(() => _autoClose = v),
        ),
      ],
    );
  }

  /// A validation message that belongs to a control with no `errorText` of its own — the date tiles.
  Widget _inlineError(String message) => Padding(
        padding: const EdgeInsets.only(top: KSpace.sm, bottom: KSpace.sm),
        child: Text(message, style: TextStyle(color: context.kurx.danger, fontSize: 13)),
      );

  Widget _buildEligibility() {
    final errors = _currentErrors;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
          _hint('Optional. Anyone turned away is told which rule stopped them, so only set a '
              'restriction the event genuinely has.'),
          // D-327 — age bounds turn away a stranger who registered. A private event has no open door
          // to turn anyone away from; attendance is the invitation list. Same rule as web.
          if (widget.product != 'Private')
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                    child: _field(_minAge, 'Minimum age',
                        keyboard: TextInputType.number, error: errors['minAge'])),
                const SizedBox(width: KSpace.md),
                Expanded(
                    child: _field(_maxAge, 'Maximum age',
                        keyboard: TextInputType.number, error: errors['maxAge'])),
              ],
            ),
          _label('Gender'),
          DropdownButtonFormField<String>(
            initialValue: _gender,
            isExpanded: true,
            decoration: const InputDecoration(border: OutlineInputBorder()),
            items: const [
              DropdownMenuItem(value: 'Any', child: Text('Open to everyone')),
              DropdownMenuItem(value: 'Male', child: Text('Male')),
              DropdownMenuItem(value: 'Female', child: Text('Female')),
              DropdownMenuItem(value: 'NonBinary', child: Text('Non-binary')),
            ],
            onChanged: (v) => setState(() => _gender = v ?? 'Any'),
          ),
          // D-327 — `teams` is Unsupported for private-gathering; a wedding has no team cap.
          if (widget.product != 'Private') ...[
            const SizedBox(height: KSpace.md),
            // A 0 is not "no cap": `ApplyFieldGroups` DISCARDS `MaxTeams <= 0`, so it silently meant
            // no cap at all. Refused here rather than swallowed.
            _field(_maxTeams, 'Maximum teams',
                keyboard: TextInputType.number, error: errors['maxTeams']),
            _hint('Total teams for the event, not teams per person.'),
          ],
      ],
    );
  }

  Widget _buildLegal() {
    final errors = _currentErrors;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
          _hint("Kurx's own terms always apply. These are your additional terms for this event."),
          _field(_termsUrl, 'Terms link',
              keyboard: TextInputType.url, error: errors['termsUrl']),
          _field(_codeOfConduct, 'Code of conduct', lines: 3),
          _field(_refundPolicy, 'Refund policy', lines: 3),
          _field(_cancellationPolicy, 'Cancellation policy', lines: 3),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            value: _requiresConsent,
            title: const Text('Require registrants to accept a statement'),
            onChanged: (v) => setState(() => _requiresConsent = v),
          ),
          // Conditionally required: switching consent on is what makes this mandatory
          // (`consent_text_required`), and switching it back off makes it optional again.
          if (_requiresConsent) ...[
            const SizedBox(height: KSpace.md),
            _field(_consentText, 'What they must accept', lines: 3, error: errors['consentText']),
            _hint('Required — acceptance is recorded against this exact wording.'),
          ],
      ],
    );
  }

  // ── Small builders ────────────────────────────────────────────────────────

  Widget _label(String text) => Padding(
        padding: const EdgeInsets.only(bottom: KSpace.sm),
        child: Text(text,
            style: Theme.of(context)
                .textTheme
                .bodyMedium
                ?.copyWith(fontWeight: FontWeight.w600, color: context.kurx.text)),
      );

  Widget _hint(String text) => Padding(
        padding: const EdgeInsets.only(bottom: KSpace.md),
        child: Text(text,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(color: context.kurx.muted)),
      );

  /// [error] is Material's own `errorText` — the framework's error affordance, which already carries
  /// the red border, the message and the screen-reader announcement. `onChanged` rebuilds on every
  /// keystroke, which is what makes the per-step result (and so Continue) track the form continuously.
  Widget _field(
    TextEditingController controller,
    String label, {
    String? hint,
    int lines = 1,
    int? maxLength,
    TextInputType? keyboard,
    String? error,
  }) =>
      Padding(
        padding: const EdgeInsets.only(bottom: KSpace.md),
        child: TextField(
          controller: controller,
          enabled: !_submitting,
          minLines: lines,
          maxLines: lines,
          maxLength: maxLength,
          keyboardType: keyboard,
          onChanged: (_) => setState(() {}),
          decoration: InputDecoration(
            labelText: label,
            hintText: hint,
            errorText: error,
            border: const OutlineInputBorder(),
            counterText: maxLength == null ? '' : null,
          ),
        ),
      );

  /// [floor] narrows what the picker will offer — see `_pickDateTime`. Absent means "now".
  Widget _dateTile(
    String label,
    DateTime? value,
    ValueChanged<DateTime> onPicked, {
    VoidCallback? onClear,
    DateTime? floor,
  }) =>
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text(label),
        subtitle: Text(
          value == null ? 'Not set' : DateFormat('EEE d MMM yyyy, h:mm a').format(value),
          style: TextStyle(color: context.kurx.muted),
        ),
        trailing: onClear != null && value != null
            ? IconButton(
                icon: const Icon(Icons.close_rounded),
                tooltip: 'Clear $label',
                onPressed: onClear,
              )
            : const Icon(Icons.edit_calendar_outlined),
        onTap: _submitting
            ? null
            : () => _pickDateTime(initial: value, onPicked: onPicked, floor: floor),
      );
}

/// One requirement the authorization letter must state. A row rather than an embedded newline, so the
/// bullet wraps and indents like a list instead of running under its own marker.
class _LetterPoint extends StatelessWidget {
  const _LetterPoint(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: 2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('• ', style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35)),
          Expanded(
            child: Text(text, style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35)),
          ),
        ],
      ),
    );
  }
}
