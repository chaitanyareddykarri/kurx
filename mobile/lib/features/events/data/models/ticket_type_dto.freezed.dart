// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'ticket_type_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

TicketTypeDto _$TicketTypeDtoFromJson(Map<String, dynamic> json) {
  return _TicketTypeDto.fromJson(json);
}

/// @nodoc
mixin _$TicketTypeDto {
  String get id => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  @JsonKey(name: 'price_paise')
  int get pricePaise => throw _privateConstructorUsedError;
  int get quantity => throw _privateConstructorUsedError;
  int get sold => throw _privateConstructorUsedError;
  int get available => throw _privateConstructorUsedError;
  @JsonKey(name: 'sale_ends')
  DateTime? get saleEnds => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_all_access')
  bool get isAllAccess => throw _privateConstructorUsedError; // ── Organiser-only: present on every response, read only by the manage screens. ──
  @JsonKey(name: 'event_id')
  String? get eventId => throw _privateConstructorUsedError;

  /// `per_ticket` or `per_group`.
  @JsonKey(name: 'pricing_unit')
  String? get pricingUnit => throw _privateConstructorUsedError;

  /// `individual` or `group`.
  @JsonKey(name: 'registration_mode')
  String? get registrationMode => throw _privateConstructorUsedError;
  @JsonKey(name: 'group_min')
  int? get groupMin => throw _privateConstructorUsedError;
  @JsonKey(name: 'group_max')
  int? get groupMax => throw _privateConstructorUsedError;

  /// D-366 — team-size price bands. Absent (not `[]`) on a ticket priced by one amount, which is
  /// every ticket that predates the decision.
  @JsonKey(name: 'price_tiers')
  List<TicketPriceTierDto>? get priceTiers =>
      throw _privateConstructorUsedError;
  @JsonKey(name: 'sale_starts')
  DateTime? get saleStarts => throw _privateConstructorUsedError;
  @JsonKey(name: 'per_user_limit')
  int? get perUserLimit => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_competition')
  bool get isCompetition => throw _privateConstructorUsedError;

  /// Serializes this TicketTypeDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TicketTypeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TicketTypeDtoCopyWith<TicketTypeDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TicketTypeDtoCopyWith<$Res> {
  factory $TicketTypeDtoCopyWith(
    TicketTypeDto value,
    $Res Function(TicketTypeDto) then,
  ) = _$TicketTypeDtoCopyWithImpl<$Res, TicketTypeDto>;
  @useResult
  $Res call({
    String id,
    String name,
    @JsonKey(name: 'price_paise') int pricePaise,
    int quantity,
    int sold,
    int available,
    @JsonKey(name: 'sale_ends') DateTime? saleEnds,
    @JsonKey(name: 'is_all_access') bool isAllAccess,
    @JsonKey(name: 'event_id') String? eventId,
    @JsonKey(name: 'pricing_unit') String? pricingUnit,
    @JsonKey(name: 'registration_mode') String? registrationMode,
    @JsonKey(name: 'group_min') int? groupMin,
    @JsonKey(name: 'group_max') int? groupMax,
    @JsonKey(name: 'price_tiers') List<TicketPriceTierDto>? priceTiers,
    @JsonKey(name: 'sale_starts') DateTime? saleStarts,
    @JsonKey(name: 'per_user_limit') int? perUserLimit,
    @JsonKey(name: 'is_competition') bool isCompetition,
  });
}

/// @nodoc
class _$TicketTypeDtoCopyWithImpl<$Res, $Val extends TicketTypeDto>
    implements $TicketTypeDtoCopyWith<$Res> {
  _$TicketTypeDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TicketTypeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? pricePaise = null,
    Object? quantity = null,
    Object? sold = null,
    Object? available = null,
    Object? saleEnds = freezed,
    Object? isAllAccess = null,
    Object? eventId = freezed,
    Object? pricingUnit = freezed,
    Object? registrationMode = freezed,
    Object? groupMin = freezed,
    Object? groupMax = freezed,
    Object? priceTiers = freezed,
    Object? saleStarts = freezed,
    Object? perUserLimit = freezed,
    Object? isCompetition = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            pricePaise: null == pricePaise
                ? _value.pricePaise
                : pricePaise // ignore: cast_nullable_to_non_nullable
                      as int,
            quantity: null == quantity
                ? _value.quantity
                : quantity // ignore: cast_nullable_to_non_nullable
                      as int,
            sold: null == sold
                ? _value.sold
                : sold // ignore: cast_nullable_to_non_nullable
                      as int,
            available: null == available
                ? _value.available
                : available // ignore: cast_nullable_to_non_nullable
                      as int,
            saleEnds: freezed == saleEnds
                ? _value.saleEnds
                : saleEnds // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            isAllAccess: null == isAllAccess
                ? _value.isAllAccess
                : isAllAccess // ignore: cast_nullable_to_non_nullable
                      as bool,
            eventId: freezed == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String?,
            pricingUnit: freezed == pricingUnit
                ? _value.pricingUnit
                : pricingUnit // ignore: cast_nullable_to_non_nullable
                      as String?,
            registrationMode: freezed == registrationMode
                ? _value.registrationMode
                : registrationMode // ignore: cast_nullable_to_non_nullable
                      as String?,
            groupMin: freezed == groupMin
                ? _value.groupMin
                : groupMin // ignore: cast_nullable_to_non_nullable
                      as int?,
            groupMax: freezed == groupMax
                ? _value.groupMax
                : groupMax // ignore: cast_nullable_to_non_nullable
                      as int?,
            priceTiers: freezed == priceTiers
                ? _value.priceTiers
                : priceTiers // ignore: cast_nullable_to_non_nullable
                      as List<TicketPriceTierDto>?,
            saleStarts: freezed == saleStarts
                ? _value.saleStarts
                : saleStarts // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            perUserLimit: freezed == perUserLimit
                ? _value.perUserLimit
                : perUserLimit // ignore: cast_nullable_to_non_nullable
                      as int?,
            isCompetition: null == isCompetition
                ? _value.isCompetition
                : isCompetition // ignore: cast_nullable_to_non_nullable
                      as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TicketTypeDtoImplCopyWith<$Res>
    implements $TicketTypeDtoCopyWith<$Res> {
  factory _$$TicketTypeDtoImplCopyWith(
    _$TicketTypeDtoImpl value,
    $Res Function(_$TicketTypeDtoImpl) then,
  ) = __$$TicketTypeDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String name,
    @JsonKey(name: 'price_paise') int pricePaise,
    int quantity,
    int sold,
    int available,
    @JsonKey(name: 'sale_ends') DateTime? saleEnds,
    @JsonKey(name: 'is_all_access') bool isAllAccess,
    @JsonKey(name: 'event_id') String? eventId,
    @JsonKey(name: 'pricing_unit') String? pricingUnit,
    @JsonKey(name: 'registration_mode') String? registrationMode,
    @JsonKey(name: 'group_min') int? groupMin,
    @JsonKey(name: 'group_max') int? groupMax,
    @JsonKey(name: 'price_tiers') List<TicketPriceTierDto>? priceTiers,
    @JsonKey(name: 'sale_starts') DateTime? saleStarts,
    @JsonKey(name: 'per_user_limit') int? perUserLimit,
    @JsonKey(name: 'is_competition') bool isCompetition,
  });
}

/// @nodoc
class __$$TicketTypeDtoImplCopyWithImpl<$Res>
    extends _$TicketTypeDtoCopyWithImpl<$Res, _$TicketTypeDtoImpl>
    implements _$$TicketTypeDtoImplCopyWith<$Res> {
  __$$TicketTypeDtoImplCopyWithImpl(
    _$TicketTypeDtoImpl _value,
    $Res Function(_$TicketTypeDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TicketTypeDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? pricePaise = null,
    Object? quantity = null,
    Object? sold = null,
    Object? available = null,
    Object? saleEnds = freezed,
    Object? isAllAccess = null,
    Object? eventId = freezed,
    Object? pricingUnit = freezed,
    Object? registrationMode = freezed,
    Object? groupMin = freezed,
    Object? groupMax = freezed,
    Object? priceTiers = freezed,
    Object? saleStarts = freezed,
    Object? perUserLimit = freezed,
    Object? isCompetition = null,
  }) {
    return _then(
      _$TicketTypeDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        pricePaise: null == pricePaise
            ? _value.pricePaise
            : pricePaise // ignore: cast_nullable_to_non_nullable
                  as int,
        quantity: null == quantity
            ? _value.quantity
            : quantity // ignore: cast_nullable_to_non_nullable
                  as int,
        sold: null == sold
            ? _value.sold
            : sold // ignore: cast_nullable_to_non_nullable
                  as int,
        available: null == available
            ? _value.available
            : available // ignore: cast_nullable_to_non_nullable
                  as int,
        saleEnds: freezed == saleEnds
            ? _value.saleEnds
            : saleEnds // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        isAllAccess: null == isAllAccess
            ? _value.isAllAccess
            : isAllAccess // ignore: cast_nullable_to_non_nullable
                  as bool,
        eventId: freezed == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String?,
        pricingUnit: freezed == pricingUnit
            ? _value.pricingUnit
            : pricingUnit // ignore: cast_nullable_to_non_nullable
                  as String?,
        registrationMode: freezed == registrationMode
            ? _value.registrationMode
            : registrationMode // ignore: cast_nullable_to_non_nullable
                  as String?,
        groupMin: freezed == groupMin
            ? _value.groupMin
            : groupMin // ignore: cast_nullable_to_non_nullable
                  as int?,
        groupMax: freezed == groupMax
            ? _value.groupMax
            : groupMax // ignore: cast_nullable_to_non_nullable
                  as int?,
        priceTiers: freezed == priceTiers
            ? _value._priceTiers
            : priceTiers // ignore: cast_nullable_to_non_nullable
                  as List<TicketPriceTierDto>?,
        saleStarts: freezed == saleStarts
            ? _value.saleStarts
            : saleStarts // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        perUserLimit: freezed == perUserLimit
            ? _value.perUserLimit
            : perUserLimit // ignore: cast_nullable_to_non_nullable
                  as int?,
        isCompetition: null == isCompetition
            ? _value.isCompetition
            : isCompetition // ignore: cast_nullable_to_non_nullable
                  as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TicketTypeDtoImpl extends _TicketTypeDto {
  const _$TicketTypeDtoImpl({
    required this.id,
    required this.name,
    @JsonKey(name: 'price_paise') this.pricePaise = 0,
    this.quantity = 0,
    this.sold = 0,
    this.available = 0,
    @JsonKey(name: 'sale_ends') this.saleEnds,
    @JsonKey(name: 'is_all_access') this.isAllAccess = false,
    @JsonKey(name: 'event_id') this.eventId,
    @JsonKey(name: 'pricing_unit') this.pricingUnit,
    @JsonKey(name: 'registration_mode') this.registrationMode,
    @JsonKey(name: 'group_min') this.groupMin,
    @JsonKey(name: 'group_max') this.groupMax,
    @JsonKey(name: 'price_tiers') final List<TicketPriceTierDto>? priceTiers,
    @JsonKey(name: 'sale_starts') this.saleStarts,
    @JsonKey(name: 'per_user_limit') this.perUserLimit,
    @JsonKey(name: 'is_competition') this.isCompetition = false,
  }) : _priceTiers = priceTiers,
       super._();

  factory _$TicketTypeDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TicketTypeDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String name;
  @override
  @JsonKey(name: 'price_paise')
  final int pricePaise;
  @override
  @JsonKey()
  final int quantity;
  @override
  @JsonKey()
  final int sold;
  @override
  @JsonKey()
  final int available;
  @override
  @JsonKey(name: 'sale_ends')
  final DateTime? saleEnds;
  @override
  @JsonKey(name: 'is_all_access')
  final bool isAllAccess;
  // ── Organiser-only: present on every response, read only by the manage screens. ──
  @override
  @JsonKey(name: 'event_id')
  final String? eventId;

  /// `per_ticket` or `per_group`.
  @override
  @JsonKey(name: 'pricing_unit')
  final String? pricingUnit;

  /// `individual` or `group`.
  @override
  @JsonKey(name: 'registration_mode')
  final String? registrationMode;
  @override
  @JsonKey(name: 'group_min')
  final int? groupMin;
  @override
  @JsonKey(name: 'group_max')
  final int? groupMax;

  /// D-366 — team-size price bands. Absent (not `[]`) on a ticket priced by one amount, which is
  /// every ticket that predates the decision.
  final List<TicketPriceTierDto>? _priceTiers;

  /// D-366 — team-size price bands. Absent (not `[]`) on a ticket priced by one amount, which is
  /// every ticket that predates the decision.
  @override
  @JsonKey(name: 'price_tiers')
  List<TicketPriceTierDto>? get priceTiers {
    final value = _priceTiers;
    if (value == null) return null;
    if (_priceTiers is EqualUnmodifiableListView) return _priceTiers;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(value);
  }

  @override
  @JsonKey(name: 'sale_starts')
  final DateTime? saleStarts;
  @override
  @JsonKey(name: 'per_user_limit')
  final int? perUserLimit;
  @override
  @JsonKey(name: 'is_competition')
  final bool isCompetition;

  @override
  String toString() {
    return 'TicketTypeDto(id: $id, name: $name, pricePaise: $pricePaise, quantity: $quantity, sold: $sold, available: $available, saleEnds: $saleEnds, isAllAccess: $isAllAccess, eventId: $eventId, pricingUnit: $pricingUnit, registrationMode: $registrationMode, groupMin: $groupMin, groupMax: $groupMax, priceTiers: $priceTiers, saleStarts: $saleStarts, perUserLimit: $perUserLimit, isCompetition: $isCompetition)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TicketTypeDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.pricePaise, pricePaise) ||
                other.pricePaise == pricePaise) &&
            (identical(other.quantity, quantity) ||
                other.quantity == quantity) &&
            (identical(other.sold, sold) || other.sold == sold) &&
            (identical(other.available, available) ||
                other.available == available) &&
            (identical(other.saleEnds, saleEnds) ||
                other.saleEnds == saleEnds) &&
            (identical(other.isAllAccess, isAllAccess) ||
                other.isAllAccess == isAllAccess) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.pricingUnit, pricingUnit) ||
                other.pricingUnit == pricingUnit) &&
            (identical(other.registrationMode, registrationMode) ||
                other.registrationMode == registrationMode) &&
            (identical(other.groupMin, groupMin) ||
                other.groupMin == groupMin) &&
            (identical(other.groupMax, groupMax) ||
                other.groupMax == groupMax) &&
            const DeepCollectionEquality().equals(
              other._priceTiers,
              _priceTiers,
            ) &&
            (identical(other.saleStarts, saleStarts) ||
                other.saleStarts == saleStarts) &&
            (identical(other.perUserLimit, perUserLimit) ||
                other.perUserLimit == perUserLimit) &&
            (identical(other.isCompetition, isCompetition) ||
                other.isCompetition == isCompetition));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    name,
    pricePaise,
    quantity,
    sold,
    available,
    saleEnds,
    isAllAccess,
    eventId,
    pricingUnit,
    registrationMode,
    groupMin,
    groupMax,
    const DeepCollectionEquality().hash(_priceTiers),
    saleStarts,
    perUserLimit,
    isCompetition,
  );

  /// Create a copy of TicketTypeDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TicketTypeDtoImplCopyWith<_$TicketTypeDtoImpl> get copyWith =>
      __$$TicketTypeDtoImplCopyWithImpl<_$TicketTypeDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$TicketTypeDtoImplToJson(this);
  }
}

abstract class _TicketTypeDto extends TicketTypeDto {
  const factory _TicketTypeDto({
    required final String id,
    required final String name,
    @JsonKey(name: 'price_paise') final int pricePaise,
    final int quantity,
    final int sold,
    final int available,
    @JsonKey(name: 'sale_ends') final DateTime? saleEnds,
    @JsonKey(name: 'is_all_access') final bool isAllAccess,
    @JsonKey(name: 'event_id') final String? eventId,
    @JsonKey(name: 'pricing_unit') final String? pricingUnit,
    @JsonKey(name: 'registration_mode') final String? registrationMode,
    @JsonKey(name: 'group_min') final int? groupMin,
    @JsonKey(name: 'group_max') final int? groupMax,
    @JsonKey(name: 'price_tiers') final List<TicketPriceTierDto>? priceTiers,
    @JsonKey(name: 'sale_starts') final DateTime? saleStarts,
    @JsonKey(name: 'per_user_limit') final int? perUserLimit,
    @JsonKey(name: 'is_competition') final bool isCompetition,
  }) = _$TicketTypeDtoImpl;
  const _TicketTypeDto._() : super._();

  factory _TicketTypeDto.fromJson(Map<String, dynamic> json) =
      _$TicketTypeDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get name;
  @override
  @JsonKey(name: 'price_paise')
  int get pricePaise;
  @override
  int get quantity;
  @override
  int get sold;
  @override
  int get available;
  @override
  @JsonKey(name: 'sale_ends')
  DateTime? get saleEnds;
  @override
  @JsonKey(name: 'is_all_access')
  bool get isAllAccess; // ── Organiser-only: present on every response, read only by the manage screens. ──
  @override
  @JsonKey(name: 'event_id')
  String? get eventId;

  /// `per_ticket` or `per_group`.
  @override
  @JsonKey(name: 'pricing_unit')
  String? get pricingUnit;

  /// `individual` or `group`.
  @override
  @JsonKey(name: 'registration_mode')
  String? get registrationMode;
  @override
  @JsonKey(name: 'group_min')
  int? get groupMin;
  @override
  @JsonKey(name: 'group_max')
  int? get groupMax;

  /// D-366 — team-size price bands. Absent (not `[]`) on a ticket priced by one amount, which is
  /// every ticket that predates the decision.
  @override
  @JsonKey(name: 'price_tiers')
  List<TicketPriceTierDto>? get priceTiers;
  @override
  @JsonKey(name: 'sale_starts')
  DateTime? get saleStarts;
  @override
  @JsonKey(name: 'per_user_limit')
  int? get perUserLimit;
  @override
  @JsonKey(name: 'is_competition')
  bool get isCompetition;

  /// Create a copy of TicketTypeDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TicketTypeDtoImplCopyWith<_$TicketTypeDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

TicketPriceTierDto _$TicketPriceTierDtoFromJson(Map<String, dynamic> json) {
  return _TicketPriceTierDto.fromJson(json);
}

/// @nodoc
mixin _$TicketPriceTierDto {
  @JsonKey(name: 'min_size')
  int get minSize => throw _privateConstructorUsedError;
  @JsonKey(name: 'max_size')
  int get maxSize => throw _privateConstructorUsedError;
  @JsonKey(name: 'price_paise')
  int get pricePaise => throw _privateConstructorUsedError;

  /// Serializes this TicketPriceTierDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TicketPriceTierDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TicketPriceTierDtoCopyWith<TicketPriceTierDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TicketPriceTierDtoCopyWith<$Res> {
  factory $TicketPriceTierDtoCopyWith(
    TicketPriceTierDto value,
    $Res Function(TicketPriceTierDto) then,
  ) = _$TicketPriceTierDtoCopyWithImpl<$Res, TicketPriceTierDto>;
  @useResult
  $Res call({
    @JsonKey(name: 'min_size') int minSize,
    @JsonKey(name: 'max_size') int maxSize,
    @JsonKey(name: 'price_paise') int pricePaise,
  });
}

/// @nodoc
class _$TicketPriceTierDtoCopyWithImpl<$Res, $Val extends TicketPriceTierDto>
    implements $TicketPriceTierDtoCopyWith<$Res> {
  _$TicketPriceTierDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TicketPriceTierDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? minSize = null,
    Object? maxSize = null,
    Object? pricePaise = null,
  }) {
    return _then(
      _value.copyWith(
            minSize: null == minSize
                ? _value.minSize
                : minSize // ignore: cast_nullable_to_non_nullable
                      as int,
            maxSize: null == maxSize
                ? _value.maxSize
                : maxSize // ignore: cast_nullable_to_non_nullable
                      as int,
            pricePaise: null == pricePaise
                ? _value.pricePaise
                : pricePaise // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TicketPriceTierDtoImplCopyWith<$Res>
    implements $TicketPriceTierDtoCopyWith<$Res> {
  factory _$$TicketPriceTierDtoImplCopyWith(
    _$TicketPriceTierDtoImpl value,
    $Res Function(_$TicketPriceTierDtoImpl) then,
  ) = __$$TicketPriceTierDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    @JsonKey(name: 'min_size') int minSize,
    @JsonKey(name: 'max_size') int maxSize,
    @JsonKey(name: 'price_paise') int pricePaise,
  });
}

/// @nodoc
class __$$TicketPriceTierDtoImplCopyWithImpl<$Res>
    extends _$TicketPriceTierDtoCopyWithImpl<$Res, _$TicketPriceTierDtoImpl>
    implements _$$TicketPriceTierDtoImplCopyWith<$Res> {
  __$$TicketPriceTierDtoImplCopyWithImpl(
    _$TicketPriceTierDtoImpl _value,
    $Res Function(_$TicketPriceTierDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TicketPriceTierDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? minSize = null,
    Object? maxSize = null,
    Object? pricePaise = null,
  }) {
    return _then(
      _$TicketPriceTierDtoImpl(
        minSize: null == minSize
            ? _value.minSize
            : minSize // ignore: cast_nullable_to_non_nullable
                  as int,
        maxSize: null == maxSize
            ? _value.maxSize
            : maxSize // ignore: cast_nullable_to_non_nullable
                  as int,
        pricePaise: null == pricePaise
            ? _value.pricePaise
            : pricePaise // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TicketPriceTierDtoImpl implements _TicketPriceTierDto {
  const _$TicketPriceTierDtoImpl({
    @JsonKey(name: 'min_size') this.minSize = 0,
    @JsonKey(name: 'max_size') this.maxSize = 0,
    @JsonKey(name: 'price_paise') this.pricePaise = 0,
  });

  factory _$TicketPriceTierDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TicketPriceTierDtoImplFromJson(json);

  @override
  @JsonKey(name: 'min_size')
  final int minSize;
  @override
  @JsonKey(name: 'max_size')
  final int maxSize;
  @override
  @JsonKey(name: 'price_paise')
  final int pricePaise;

  @override
  String toString() {
    return 'TicketPriceTierDto(minSize: $minSize, maxSize: $maxSize, pricePaise: $pricePaise)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TicketPriceTierDtoImpl &&
            (identical(other.minSize, minSize) || other.minSize == minSize) &&
            (identical(other.maxSize, maxSize) || other.maxSize == maxSize) &&
            (identical(other.pricePaise, pricePaise) ||
                other.pricePaise == pricePaise));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, minSize, maxSize, pricePaise);

  /// Create a copy of TicketPriceTierDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TicketPriceTierDtoImplCopyWith<_$TicketPriceTierDtoImpl> get copyWith =>
      __$$TicketPriceTierDtoImplCopyWithImpl<_$TicketPriceTierDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$TicketPriceTierDtoImplToJson(this);
  }
}

abstract class _TicketPriceTierDto implements TicketPriceTierDto {
  const factory _TicketPriceTierDto({
    @JsonKey(name: 'min_size') final int minSize,
    @JsonKey(name: 'max_size') final int maxSize,
    @JsonKey(name: 'price_paise') final int pricePaise,
  }) = _$TicketPriceTierDtoImpl;

  factory _TicketPriceTierDto.fromJson(Map<String, dynamic> json) =
      _$TicketPriceTierDtoImpl.fromJson;

  @override
  @JsonKey(name: 'min_size')
  int get minSize;
  @override
  @JsonKey(name: 'max_size')
  int get maxSize;
  @override
  @JsonKey(name: 'price_paise')
  int get pricePaise;

  /// Create a copy of TicketPriceTierDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TicketPriceTierDtoImplCopyWith<_$TicketPriceTierDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
