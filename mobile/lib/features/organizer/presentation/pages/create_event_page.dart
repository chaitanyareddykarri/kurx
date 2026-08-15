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
import '../../domain/event_wizard_payload.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
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

  /// Already answered in the gate (D-343): the free/paid pair is what selected the verification tier
  /// the caller just cleared. Carried in as the starting value rather than re-asked from scratch, and
  /// still changeable here — the Paid card stays gated on `canOrganizePaid`, so moving to Paid after
  /// entering as Free cannot escape the financial tier.
  final String initialPricing;

  @override
  ConsumerState<CreateEventPage> createState() => _CreateEventPageState();
}

/// The same eleven steps as web, in the same order (D-305). They were seven here — `basics` collapsed
/// visibility, category, type and details into one screen — which is how Flutter ended up silently
/// missing description, venue address, capacity, the two certificate/result dates, cancellation policy
/// and the whole pricing step. One product, one flow: a person who learns this on web must recognise it
/// here.
enum _Step {
  representing, visibility, pricing, category, type, details,
  content, location, windows, eligibility, legal,
  /// D-351 — the represented institution's written consent, asked in-flow rather than on a separate
  /// screen after the draft exists. Appended last so every earlier step keeps its position, and only
  /// present for a Public event that represents an institution (see `_steps`).
  authorization
}

const _stepTitles = <_Step, String>{
  _Step.representing: 'Representing',
  _Step.visibility: 'Who can see it',
  _Step.pricing: 'Pricing',
  _Step.category: 'Category',
  _Step.type: 'Type',
  _Step.details: 'The basics',
  _Step.content: 'How it reads',
  _Step.location: 'Where it happens',
  _Step.windows: 'Key dates',
  _Step.eligibility: 'Who can join',
  _Step.legal: 'Terms',
  _Step.authorization: 'Authorization',
};

class _CreateEventPageState extends ConsumerState<CreateEventPage> {
  // ── Representing ──────────────────────────────────────────────────────────
  /// null = Personal — the user represents themselves. Not "an organisation that is personal": there
  /// is no organisation in that branch at all, and the client neither names nor creates one (D-268).
  String? _representingOrgId;

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
  /// Free / paid. A capability gate, exactly as on web — it creates no ticket by itself.
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

  /// D-351 — Authorization is present only for a Public event that represents an institution, which is
  /// exactly the shape `PolicyResolver` raises `event_authorization_required` for. A self-represented
  /// event has no institution to authorise it and never sees the step.
  bool get _needsAuthorization =>
      widget.product == 'Public' && _representingOrgId != null;

  List<_Step> get _steps => _needsAuthorization
      ? _Step.values
      : _Step.values.where((s) => s != _Step.authorization).toList();

  /// Mirrors web's `missingForSubmit()` for this step, and the server's own refusals
  /// (`authorization_fields_required`, `letterhead_required`, `representative_role_other_required`).
  bool get _authorizationValid =>
      !_needsAuthorization ||
      (_headName.text.trim().isNotEmpty &&
          _headDesignation.text.trim().isNotEmpty &&
          _officialEmail.text.trim().isNotEmpty &&
          _officialPhone.text.trim().isNotEmpty &&
          (_representativeRole?.isNotEmpty ?? false) &&
          (_representativeRole != 'Other' || _representativeRoleOther.text.trim().isNotEmpty) &&
          _letterBytes != null);

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
      _ticketName, _ticketPrice, _ticketQuantity,
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

  bool get _basicsValid =>
      _title.text.trim().isNotEmpty &&
      _categoryId != null &&
      (!_categoryHasTypes || _typeId != null) &&
      _endsAt.isAfter(_startsAt);

  /// Mirrors the server's `consent_text_required`: asking people to accept an empty string would
  /// record a consent that evidences nothing.
  bool get _consentOk => !_requiresConsent || _consentText.text.trim().isNotEmpty;

  /// Rupees → paise. A blank price is free (0), never absent.
  int get _ticketPricePaise =>
      ((double.tryParse(_ticketPrice.text.trim()) ?? 0) * 100).round();

  /// A ticket must be nameable and countable. Price may be 0 (free) but never negative.
  bool get _ticketValid =>
      _ticketName.text.trim().isNotEmpty &&
      (int.tryParse(_ticketQuantity.text.trim()) ?? 0) > 0 &&
      _ticketPricePaise >= 0;

  /// D-350 — a paid event must represent a VERIFIED organisation; hosting as yourself has no account
  /// for the money to settle into, and the server refuses it at submit-for-review. Free events keep the
  /// always-valid default (self). Mirrors web's `representingValid`.
  ///
  /// Failing CLOSED on a representation list that has not loaded: unlike the type lookup above, an
  /// absent answer here means "we do not know that a verified organisation exists", and letting a paid
  /// event through on that assumption is the failure this guard exists to prevent.
  bool get _representingValid =>
      widget.product == 'Private' ||
      // D-352 — the server states whether an organisation is required at all.
      !(ref.read(currentUserProvider)?.trust.requiresRepresentation ?? true) ||
      ref.read(myRepresentationsProvider).maybeWhen(
            data: (list) => list.any((r) =>
                r.organizationId == _representingOrgId && (r.canBackPaidEvent || r.isVerified)),
            orElse: () => false,
          );

  bool get _canSubmit => _basicsValid && _consentOk && _ticketValid && _representingValid
      && _authorizationValid && !_submitting;

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
          // Rupees in, paise on the wire (D-004). Free is genuinely 0, never absent.
          'pricePaise': _ticketPricePaise,
          'pricingUnit': 'PerTicket',
          'registrationMode': 'Individual',
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
      if (_needsAuthorization) {
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

  Future<void> _pickDateTime({
    required DateTime? initial,
    required ValueChanged<DateTime> onPicked,
  }) async {
    final base = initial ?? DateTime.now().add(const Duration(days: 1));
    final date = await showDatePicker(
      context: context,
      initialDate: base,
      firstDate: DateTime.now().subtract(const Duration(days: 1)),
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
              _Step.pricing => _buildPricing(),
              _Step.category => _buildCategory(),
              _Step.type => _buildType(),
              _Step.details => _buildDetails(),
              _Step.content => _buildContent(),
              _Step.location => _buildLocation(),
              _Step.windows => _buildWindows(),
              _Step.eligibility => _buildEligibility(),
              _Step.legal => _buildLegal(),
              _Step.authorization => _buildAuthorization(),
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
                  // Basics gates progress, and since D-350 so does Representing — but only for a PAID
                  // event, where hosting as yourself is not a legal answer at all. Pricing re-checks it
                  // because someone can pass Representing as themselves while Free, then switch to Paid
                  // one step later and carry the stale answer to a submission the server refuses.
                  // Every other step stays ungated: blocking inside an optional step is how a wizard
                  // gets abandoned.
                  onPressed: isLast
                      ? (_canSubmit ? _submit : null)
                      : ((_step == _Step.details && !_basicsValid) ||
                              ((_step == _Step.representing || _step == _Step.pricing) &&
                                  !_representingValid)
                          ? null
                          : () => setState(() => _step = steps[index + 1])),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  // ── Steps ─────────────────────────────────────────────────────────────────

  /// Who the event is hosted as. Personal is always available and is the default, which is the whole
  /// reason this screen no longer needs an organisation chosen before it opens (D-267).
  /// D-353 — a PUBLIC event must represent a verified organisation; there is no self-hosting card.
  /// A PRIVATE event reaches no discovery surface and can never sell, so it is hosted by the person —
  /// and is deliberately NOT presented as an organisation of any kind, because self-representation is
  /// not a concept in this model. Mirrors web's Representing step.
  Widget _buildRepresenting() {
    final c = context.kurx;
    if (widget.product == 'Private') {
      return Container(
        padding: const EdgeInsets.all(KSpace.md),
        decoration: BoxDecoration(
          border: Border.all(color: c.border),
          borderRadius: BorderRadius.circular(KRadius.md),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Hosted by you', style: TextStyle(color: c.text, fontSize: 14)),
            const SizedBox(height: KSpace.xs),
            Text(
              "A private event is invitation-only, never appears in search or on Home, and can't sell "
              "tickets — so there's no organisation to name and nothing to verify.",
              style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
            ),
          ],
        ),
      );
    }

    final reps = ref.watch(myRepresentationsProvider);
    return reps.maybeWhen(
      data: (list) {
        // A PendingReview organisation is deliberately NOT selectable: a staged representation is not
        // an approved one. Shown below so nobody re-requests something already in the queue.
        final selectable = list.where((r) => r.canBackPaidEvent || r.isVerified).toList();
        final pending = list.where((r) => !(r.canBackPaidEvent || r.isVerified)).toList();

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
                    Text("You don't currently have an approved organisation representation.",
                        style: TextStyle(color: c.text, fontSize: 14)),
                    const SizedBox(height: KSpace.xs),
                    Text(
                      'A public event has to be hosted on behalf of an organisation Kurx has verified. '
                      "Request representation and submit the organisation's official authorisation — an "
                      'admin reviews it before it can be used.',
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
                        subtitle: Text('Representing · your authority: ${r.authority}'),
                      ),
                  ],
                ),
              ),
            ],
            for (final r in pending) ...[
              const SizedBox(height: KSpace.sm),
              Text('${r.name} — awaiting verification. It cannot host a public event until an admin '
                  'approves it.',
                  style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35)),
            ],
            const SizedBox(height: KSpace.md),
            Text(
              'Representing an organisation you do not see here? Request representation from your '
              'profile.',
              style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
            ),
          ],
        );
      },
      orElse: () => const Center(child: CircularProgressIndicator()),
    );
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

  /// The event's first ticket (D-305), asked here rather than left to the manage screen.
  ///
  /// Web asks the same three things in its Pricing step. Without a ticket type the event cannot be
  /// registered for at all, so a wizard that skipped this produced an event nobody could join and said
  /// nothing about it.
  Widget _buildTicket() {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SizedBox(height: KSpace.lg),
        _label('Your ticket'),
        Text(
          'Every event needs at least one ticket before anyone can register. Leave the price blank for a free event; add more tiers later.',
          style: TextStyle(color: c.muted, fontSize: 12.5, height: 1.35),
        ),
        const SizedBox(height: KSpace.sm),
        _field(_ticketName, 'Ticket name'),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (widget.product != 'Private') ...[
              Expanded(child: _field(_ticketPrice, 'Price (₹)', keyboard: TextInputType.number)),
              const SizedBox(width: KSpace.md),
            ],
            Expanded(child: _field(_ticketQuantity, 'How many', keyboard: TextInputType.number)),
          ],
        ),
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

  /// Free / paid, plus the event's first ticket. Mirrors web's Pricing step: the free/paid choice is a
  /// capability gate and creates nothing by itself; the ticket below it is what makes the event
  /// bookable at all.
  Widget _buildPricing() {
    // A Private product cannot take payment (`private_product_cannot_take_payment`), so Paid is not
    // offered at all — and the price field with it.
    final canChoosePaid = widget.product == 'Public' &&
        (ref.watch(currentUserProvider)?.trust.canOrganizePaid ?? false);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RadioGroup<String>(
          groupValue: _pricing,
          onChanged: (v) => setState(() => _pricing = v ?? 'free'),
          child: Column(
            children: [
              const RadioListTile<String>(
                value: 'free',
                title: Text('Free'),
                subtitle: Text('No ticket charges. Anyone can register.'),
              ),
              RadioListTile<String>(
                value: 'paid',
                enabled: canChoosePaid,
                title: const Text('Paid'),
                subtitle: Text(widget.product == 'Private'
                    ? "Private events are always free — they can't sell tickets."
                    : canChoosePaid
                        ? 'Sell tickets. Set the price below.'
                        : 'Needs identity, PAN and a verified bank account.'),
              ),
            ],
          ),
        ),
        _buildTicket(),
      ],
    );
  }

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

  Widget _buildDetails() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _field(_title, 'Title', hint: 'What is your event called?'),
          _field(_subtitle, 'Subtitle', hint: 'One line that sells it (optional)'),
          _field(_description, 'Description', lines: 4,
              hint: 'Required before you can publish'),
          _label('When'),
          _dateTile('Starts', _startsAt, (d) => setState(() {
                _startsAt = d;
                if (!_endsAt.isAfter(_startsAt)) _endsAt = _startsAt.add(const Duration(hours: 3));
              })),
          _dateTile('Ends', _endsAt, (d) => setState(() => _endsAt = d)),
          if (!_endsAt.isAfter(_startsAt))
            Padding(
              padding: const EdgeInsets.only(top: KSpace.sm, bottom: KSpace.sm),
              child: Text('End must be after the start.',
                  style: TextStyle(color: context.kurx.danger, fontSize: 13)),
            ),
          _field(_venueName, 'Venue name'),
          _field(_city, 'City'),
          _field(_venueAddress, 'Venue address'),
          _field(_capacity, 'Capacity', keyboard: TextInputType.number),
        ],
      );

  Widget _buildContent() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _hint('Optional, but this is what a listing card and a shared link show.'),
          _field(_tagline, 'Tagline', maxLength: 160),
          _field(_shortDescription, 'Short description', maxLength: 300, lines: 2),
          _field(_rules, 'Rules', lines: 4),
        ],
      );

  Widget _buildLocation() => Column(
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
            _field(_mapsUrl, 'Google Maps link', keyboard: TextInputType.url),
          ],
          if (_eventMode != 'Offline') ...[
            _field(_onlineUrl, 'Join link', keyboard: TextInputType.url),
            _field(_meetingPlatform, 'Platform', hint: 'Zoom, Meet, Teams…'),
            _field(_meetingPassword, 'Meeting password'),
            _hint('The password is only shown to confirmed registrants.'),
          ],
        ],
      );

  Widget _buildWindows() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _hint("All optional. Registration times bound every ticket type; a ticket's own sale "
              'window can narrow that further, never widen it.'),
          _label('Registration'),
          _dateTile('Opens', _registrationOpensAt, (d) => setState(() => _registrationOpensAt = d),
              onClear: () => setState(() => _registrationOpensAt = null)),
          _dateTile('Closes', _registrationClosesAt, (d) => setState(() => _registrationClosesAt = d),
              onClear: () => setState(() => _registrationClosesAt = null)),
          const SizedBox(height: KSpace.md),
          _label('Check-in'),
          _dateTile('Opens', _checkinOpensAt, (d) => setState(() => _checkinOpensAt = d),
              onClear: () => setState(() => _checkinOpensAt = null)),
          _dateTile('Closes', _checkinClosesAt, (d) => setState(() => _checkinClosesAt = d),
              onClear: () => setState(() => _checkinClosesAt = null)),
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

  Widget _buildEligibility() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _hint('Optional. Anyone turned away is told which rule stopped them, so only set a '
              'restriction the event genuinely has.'),
          // D-327 — age bounds turn away a stranger who registered. A private event has no open door
          // to turn anyone away from; attendance is the invitation list. Same rule as web.
          if (widget.product != 'Private')
            Row(
              children: [
                Expanded(child: _field(_minAge, 'Minimum age', keyboard: TextInputType.number)),
                const SizedBox(width: KSpace.md),
                Expanded(child: _field(_maxAge, 'Maximum age', keyboard: TextInputType.number)),
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
            _field(_maxTeams, 'Maximum teams', keyboard: TextInputType.number),
            _hint('Total teams for the event, not teams per person.'),
          ],
        ],
      );

  Widget _buildLegal() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _hint("Kurx's own terms always apply. These are your additional terms for this event."),
          _field(_termsUrl, 'Terms link', keyboard: TextInputType.url),
          _field(_codeOfConduct, 'Code of conduct', lines: 3),
          _field(_refundPolicy, 'Refund policy', lines: 3),
          _field(_cancellationPolicy, 'Cancellation policy', lines: 3),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            value: _requiresConsent,
            title: const Text('Require registrants to accept a statement'),
            onChanged: (v) => setState(() => _requiresConsent = v),
          ),
          if (_requiresConsent) ...[
            const SizedBox(height: KSpace.md),
            _field(_consentText, 'What they must accept', lines: 3),
            _hint('Required — acceptance is recorded against this exact wording.'),
          ],
        ],
      );

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

  Widget _field(
    TextEditingController controller,
    String label, {
    String? hint,
    int lines = 1,
    int? maxLength,
    TextInputType? keyboard,
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
            border: const OutlineInputBorder(),
            counterText: maxLength == null ? '' : null,
          ),
        ),
      );

  Widget _dateTile(
    String label,
    DateTime? value,
    ValueChanged<DateTime> onPicked, {
    VoidCallback? onClear,
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
        onTap: _submitting ? null : () => _pickDateTime(initial: value, onPicked: onPicked),
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
