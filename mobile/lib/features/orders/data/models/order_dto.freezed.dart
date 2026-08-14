// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'order_dto.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

TicketDto _$TicketDtoFromJson(Map<String, dynamic> json) {
  return _TicketDto.fromJson(json);
}

/// @nodoc
mixin _$TicketDto {
  String get id => throw _privateConstructorUsedError;
  String get code => throw _privateConstructorUsedError;
  String get state => throw _privateConstructorUsedError;
  @JsonKey(name: 'checked_in_at')
  DateTime? get checkedInAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'answers_json')
  String? get answersJson => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime get createdAt => throw _privateConstructorUsedError;

  /// Serializes this TicketDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TicketDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TicketDtoCopyWith<TicketDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TicketDtoCopyWith<$Res> {
  factory $TicketDtoCopyWith(TicketDto value, $Res Function(TicketDto) then) =
      _$TicketDtoCopyWithImpl<$Res, TicketDto>;
  @useResult
  $Res call({
    String id,
    String code,
    String state,
    @JsonKey(name: 'checked_in_at') DateTime? checkedInAt,
    @JsonKey(name: 'answers_json') String? answersJson,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class _$TicketDtoCopyWithImpl<$Res, $Val extends TicketDto>
    implements $TicketDtoCopyWith<$Res> {
  _$TicketDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TicketDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? code = null,
    Object? state = null,
    Object? checkedInAt = freezed,
    Object? answersJson = freezed,
    Object? createdAt = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            code: null == code
                ? _value.code
                : code // ignore: cast_nullable_to_non_nullable
                      as String,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            checkedInAt: freezed == checkedInAt
                ? _value.checkedInAt
                : checkedInAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            answersJson: freezed == answersJson
                ? _value.answersJson
                : answersJson // ignore: cast_nullable_to_non_nullable
                      as String?,
            createdAt: null == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TicketDtoImplCopyWith<$Res>
    implements $TicketDtoCopyWith<$Res> {
  factory _$$TicketDtoImplCopyWith(
    _$TicketDtoImpl value,
    $Res Function(_$TicketDtoImpl) then,
  ) = __$$TicketDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String code,
    String state,
    @JsonKey(name: 'checked_in_at') DateTime? checkedInAt,
    @JsonKey(name: 'answers_json') String? answersJson,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class __$$TicketDtoImplCopyWithImpl<$Res>
    extends _$TicketDtoCopyWithImpl<$Res, _$TicketDtoImpl>
    implements _$$TicketDtoImplCopyWith<$Res> {
  __$$TicketDtoImplCopyWithImpl(
    _$TicketDtoImpl _value,
    $Res Function(_$TicketDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TicketDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? code = null,
    Object? state = null,
    Object? checkedInAt = freezed,
    Object? answersJson = freezed,
    Object? createdAt = null,
  }) {
    return _then(
      _$TicketDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        code: null == code
            ? _value.code
            : code // ignore: cast_nullable_to_non_nullable
                  as String,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        checkedInAt: freezed == checkedInAt
            ? _value.checkedInAt
            : checkedInAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        answersJson: freezed == answersJson
            ? _value.answersJson
            : answersJson // ignore: cast_nullable_to_non_nullable
                  as String?,
        createdAt: null == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TicketDtoImpl implements _TicketDto {
  const _$TicketDtoImpl({
    required this.id,
    required this.code,
    required this.state,
    @JsonKey(name: 'checked_in_at') this.checkedInAt,
    @JsonKey(name: 'answers_json') this.answersJson,
    @JsonKey(name: 'created_at') required this.createdAt,
  });

  factory _$TicketDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TicketDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String code;
  @override
  final String state;
  @override
  @JsonKey(name: 'checked_in_at')
  final DateTime? checkedInAt;
  @override
  @JsonKey(name: 'answers_json')
  final String? answersJson;
  @override
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  @override
  String toString() {
    return 'TicketDto(id: $id, code: $code, state: $state, checkedInAt: $checkedInAt, answersJson: $answersJson, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TicketDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.code, code) || other.code == code) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.checkedInAt, checkedInAt) ||
                other.checkedInAt == checkedInAt) &&
            (identical(other.answersJson, answersJson) ||
                other.answersJson == answersJson) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    code,
    state,
    checkedInAt,
    answersJson,
    createdAt,
  );

  /// Create a copy of TicketDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TicketDtoImplCopyWith<_$TicketDtoImpl> get copyWith =>
      __$$TicketDtoImplCopyWithImpl<_$TicketDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$TicketDtoImplToJson(this);
  }
}

abstract class _TicketDto implements TicketDto {
  const factory _TicketDto({
    required final String id,
    required final String code,
    required final String state,
    @JsonKey(name: 'checked_in_at') final DateTime? checkedInAt,
    @JsonKey(name: 'answers_json') final String? answersJson,
    @JsonKey(name: 'created_at') required final DateTime createdAt,
  }) = _$TicketDtoImpl;

  factory _TicketDto.fromJson(Map<String, dynamic> json) =
      _$TicketDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get code;
  @override
  String get state;
  @override
  @JsonKey(name: 'checked_in_at')
  DateTime? get checkedInAt;
  @override
  @JsonKey(name: 'answers_json')
  String? get answersJson;
  @override
  @JsonKey(name: 'created_at')
  DateTime get createdAt;

  /// Create a copy of TicketDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TicketDtoImplCopyWith<_$TicketDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

OrderDto _$OrderDtoFromJson(Map<String, dynamic> json) {
  return _OrderDto.fromJson(json);
}

/// @nodoc
mixin _$OrderDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'ticket_type_id')
  String get ticketTypeId => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'amount_paise')
  int get amountPaise => throw _privateConstructorUsedError; // ISO-4217, the event's settlement currency (V3 §9.1, D-257). Defaulted rather than required so an
  // app build newer than its server still parses — the server only began sending this alongside D-245.
  String get currency => throw _privateConstructorUsedError;
  @JsonKey(name: 'razorpay_order_id')
  String? get razorpayOrderId => throw _privateConstructorUsedError;
  @JsonKey(name: 'group_id')
  String? get groupId => throw _privateConstructorUsedError;
  @JsonKey(name: 'join_code')
  String? get joinCode => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime get createdAt => throw _privateConstructorUsedError;
  List<TicketDto> get tickets => throw _privateConstructorUsedError;
  @JsonKey(name: 'guest_access_token')
  String? get guestAccessToken => throw _privateConstructorUsedError;

  /// Serializes this OrderDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of OrderDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $OrderDtoCopyWith<OrderDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $OrderDtoCopyWith<$Res> {
  factory $OrderDtoCopyWith(OrderDto value, $Res Function(OrderDto) then) =
      _$OrderDtoCopyWithImpl<$Res, OrderDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String ticketTypeId,
    String status,
    @JsonKey(name: 'amount_paise') int amountPaise,
    String currency,
    @JsonKey(name: 'razorpay_order_id') String? razorpayOrderId,
    @JsonKey(name: 'group_id') String? groupId,
    @JsonKey(name: 'join_code') String? joinCode,
    @JsonKey(name: 'created_at') DateTime createdAt,
    List<TicketDto> tickets,
    @JsonKey(name: 'guest_access_token') String? guestAccessToken,
  });
}

/// @nodoc
class _$OrderDtoCopyWithImpl<$Res, $Val extends OrderDto>
    implements $OrderDtoCopyWith<$Res> {
  _$OrderDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of OrderDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = null,
    Object? status = null,
    Object? amountPaise = null,
    Object? currency = null,
    Object? razorpayOrderId = freezed,
    Object? groupId = freezed,
    Object? joinCode = freezed,
    Object? createdAt = null,
    Object? tickets = null,
    Object? guestAccessToken = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: null == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String,
            ticketTypeId: null == ticketTypeId
                ? _value.ticketTypeId
                : ticketTypeId // ignore: cast_nullable_to_non_nullable
                      as String,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            amountPaise: null == amountPaise
                ? _value.amountPaise
                : amountPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            currency: null == currency
                ? _value.currency
                : currency // ignore: cast_nullable_to_non_nullable
                      as String,
            razorpayOrderId: freezed == razorpayOrderId
                ? _value.razorpayOrderId
                : razorpayOrderId // ignore: cast_nullable_to_non_nullable
                      as String?,
            groupId: freezed == groupId
                ? _value.groupId
                : groupId // ignore: cast_nullable_to_non_nullable
                      as String?,
            joinCode: freezed == joinCode
                ? _value.joinCode
                : joinCode // ignore: cast_nullable_to_non_nullable
                      as String?,
            createdAt: null == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            tickets: null == tickets
                ? _value.tickets
                : tickets // ignore: cast_nullable_to_non_nullable
                      as List<TicketDto>,
            guestAccessToken: freezed == guestAccessToken
                ? _value.guestAccessToken
                : guestAccessToken // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$OrderDtoImplCopyWith<$Res>
    implements $OrderDtoCopyWith<$Res> {
  factory _$$OrderDtoImplCopyWith(
    _$OrderDtoImpl value,
    $Res Function(_$OrderDtoImpl) then,
  ) = __$$OrderDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String ticketTypeId,
    String status,
    @JsonKey(name: 'amount_paise') int amountPaise,
    String currency,
    @JsonKey(name: 'razorpay_order_id') String? razorpayOrderId,
    @JsonKey(name: 'group_id') String? groupId,
    @JsonKey(name: 'join_code') String? joinCode,
    @JsonKey(name: 'created_at') DateTime createdAt,
    List<TicketDto> tickets,
    @JsonKey(name: 'guest_access_token') String? guestAccessToken,
  });
}

/// @nodoc
class __$$OrderDtoImplCopyWithImpl<$Res>
    extends _$OrderDtoCopyWithImpl<$Res, _$OrderDtoImpl>
    implements _$$OrderDtoImplCopyWith<$Res> {
  __$$OrderDtoImplCopyWithImpl(
    _$OrderDtoImpl _value,
    $Res Function(_$OrderDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of OrderDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = null,
    Object? status = null,
    Object? amountPaise = null,
    Object? currency = null,
    Object? razorpayOrderId = freezed,
    Object? groupId = freezed,
    Object? joinCode = freezed,
    Object? createdAt = null,
    Object? tickets = null,
    Object? guestAccessToken = freezed,
  }) {
    return _then(
      _$OrderDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        ticketTypeId: null == ticketTypeId
            ? _value.ticketTypeId
            : ticketTypeId // ignore: cast_nullable_to_non_nullable
                  as String,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        amountPaise: null == amountPaise
            ? _value.amountPaise
            : amountPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        currency: null == currency
            ? _value.currency
            : currency // ignore: cast_nullable_to_non_nullable
                  as String,
        razorpayOrderId: freezed == razorpayOrderId
            ? _value.razorpayOrderId
            : razorpayOrderId // ignore: cast_nullable_to_non_nullable
                  as String?,
        groupId: freezed == groupId
            ? _value.groupId
            : groupId // ignore: cast_nullable_to_non_nullable
                  as String?,
        joinCode: freezed == joinCode
            ? _value.joinCode
            : joinCode // ignore: cast_nullable_to_non_nullable
                  as String?,
        createdAt: null == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        tickets: null == tickets
            ? _value._tickets
            : tickets // ignore: cast_nullable_to_non_nullable
                  as List<TicketDto>,
        guestAccessToken: freezed == guestAccessToken
            ? _value.guestAccessToken
            : guestAccessToken // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$OrderDtoImpl implements _OrderDto {
  const _$OrderDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'ticket_type_id') required this.ticketTypeId,
    required this.status,
    @JsonKey(name: 'amount_paise') required this.amountPaise,
    this.currency = 'INR',
    @JsonKey(name: 'razorpay_order_id') this.razorpayOrderId,
    @JsonKey(name: 'group_id') this.groupId,
    @JsonKey(name: 'join_code') this.joinCode,
    @JsonKey(name: 'created_at') required this.createdAt,
    final List<TicketDto> tickets = const [],
    @JsonKey(name: 'guest_access_token') this.guestAccessToken,
  }) : _tickets = tickets;

  factory _$OrderDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$OrderDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  final String ticketTypeId;
  @override
  final String status;
  @override
  @JsonKey(name: 'amount_paise')
  final int amountPaise;
  // ISO-4217, the event's settlement currency (V3 §9.1, D-257). Defaulted rather than required so an
  // app build newer than its server still parses — the server only began sending this alongside D-245.
  @override
  @JsonKey()
  final String currency;
  @override
  @JsonKey(name: 'razorpay_order_id')
  final String? razorpayOrderId;
  @override
  @JsonKey(name: 'group_id')
  final String? groupId;
  @override
  @JsonKey(name: 'join_code')
  final String? joinCode;
  @override
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  final List<TicketDto> _tickets;
  @override
  @JsonKey()
  List<TicketDto> get tickets {
    if (_tickets is EqualUnmodifiableListView) return _tickets;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_tickets);
  }

  @override
  @JsonKey(name: 'guest_access_token')
  final String? guestAccessToken;

  @override
  String toString() {
    return 'OrderDto(id: $id, eventId: $eventId, ticketTypeId: $ticketTypeId, status: $status, amountPaise: $amountPaise, currency: $currency, razorpayOrderId: $razorpayOrderId, groupId: $groupId, joinCode: $joinCode, createdAt: $createdAt, tickets: $tickets, guestAccessToken: $guestAccessToken)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$OrderDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.ticketTypeId, ticketTypeId) ||
                other.ticketTypeId == ticketTypeId) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.amountPaise, amountPaise) ||
                other.amountPaise == amountPaise) &&
            (identical(other.currency, currency) ||
                other.currency == currency) &&
            (identical(other.razorpayOrderId, razorpayOrderId) ||
                other.razorpayOrderId == razorpayOrderId) &&
            (identical(other.groupId, groupId) || other.groupId == groupId) &&
            (identical(other.joinCode, joinCode) ||
                other.joinCode == joinCode) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt) &&
            const DeepCollectionEquality().equals(other._tickets, _tickets) &&
            (identical(other.guestAccessToken, guestAccessToken) ||
                other.guestAccessToken == guestAccessToken));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    ticketTypeId,
    status,
    amountPaise,
    currency,
    razorpayOrderId,
    groupId,
    joinCode,
    createdAt,
    const DeepCollectionEquality().hash(_tickets),
    guestAccessToken,
  );

  /// Create a copy of OrderDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$OrderDtoImplCopyWith<_$OrderDtoImpl> get copyWith =>
      __$$OrderDtoImplCopyWithImpl<_$OrderDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$OrderDtoImplToJson(this);
  }
}

abstract class _OrderDto implements OrderDto {
  const factory _OrderDto({
    required final String id,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'ticket_type_id') required final String ticketTypeId,
    required final String status,
    @JsonKey(name: 'amount_paise') required final int amountPaise,
    final String currency,
    @JsonKey(name: 'razorpay_order_id') final String? razorpayOrderId,
    @JsonKey(name: 'group_id') final String? groupId,
    @JsonKey(name: 'join_code') final String? joinCode,
    @JsonKey(name: 'created_at') required final DateTime createdAt,
    final List<TicketDto> tickets,
    @JsonKey(name: 'guest_access_token') final String? guestAccessToken,
  }) = _$OrderDtoImpl;

  factory _OrderDto.fromJson(Map<String, dynamic> json) =
      _$OrderDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  String get ticketTypeId;
  @override
  String get status;
  @override
  @JsonKey(name: 'amount_paise')
  int get amountPaise; // ISO-4217, the event's settlement currency (V3 §9.1, D-257). Defaulted rather than required so an
  // app build newer than its server still parses — the server only began sending this alongside D-245.
  @override
  String get currency;
  @override
  @JsonKey(name: 'razorpay_order_id')
  String? get razorpayOrderId;
  @override
  @JsonKey(name: 'group_id')
  String? get groupId;
  @override
  @JsonKey(name: 'join_code')
  String? get joinCode;
  @override
  @JsonKey(name: 'created_at')
  DateTime get createdAt;
  @override
  List<TicketDto> get tickets;
  @override
  @JsonKey(name: 'guest_access_token')
  String? get guestAccessToken;

  /// Create a copy of OrderDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$OrderDtoImplCopyWith<_$OrderDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

GroupMemberDto _$GroupMemberDtoFromJson(Map<String, dynamic> json) {
  return _GroupMemberDto.fromJson(json);
}

/// @nodoc
mixin _$GroupMemberDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'user_id')
  String? get userId => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String get phone => throw _privateConstructorUsedError;
  @JsonKey(name: 'ticket_id')
  String? get ticketId => throw _privateConstructorUsedError;
  @JsonKey(name: 'answers_json')
  String? get answersJson => throw _privateConstructorUsedError;
  @JsonKey(name: 'joined_at')
  DateTime? get joinedAt => throw _privateConstructorUsedError;
  String? get username => throw _privateConstructorUsedError;
  @JsonKey(name: 'avatar_key')
  String? get avatarKey => throw _privateConstructorUsedError;

  /// Serializes this GroupMemberDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of GroupMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $GroupMemberDtoCopyWith<GroupMemberDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $GroupMemberDtoCopyWith<$Res> {
  factory $GroupMemberDtoCopyWith(
    GroupMemberDto value,
    $Res Function(GroupMemberDto) then,
  ) = _$GroupMemberDtoCopyWithImpl<$Res, GroupMemberDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'user_id') String? userId,
    String name,
    String phone,
    @JsonKey(name: 'ticket_id') String? ticketId,
    @JsonKey(name: 'answers_json') String? answersJson,
    @JsonKey(name: 'joined_at') DateTime? joinedAt,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
  });
}

/// @nodoc
class _$GroupMemberDtoCopyWithImpl<$Res, $Val extends GroupMemberDto>
    implements $GroupMemberDtoCopyWith<$Res> {
  _$GroupMemberDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of GroupMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? userId = freezed,
    Object? name = null,
    Object? phone = null,
    Object? ticketId = freezed,
    Object? answersJson = freezed,
    Object? joinedAt = freezed,
    Object? username = freezed,
    Object? avatarKey = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            userId: freezed == userId
                ? _value.userId
                : userId // ignore: cast_nullable_to_non_nullable
                      as String?,
            name: null == name
                ? _value.name
                : name // ignore: cast_nullable_to_non_nullable
                      as String,
            phone: null == phone
                ? _value.phone
                : phone // ignore: cast_nullable_to_non_nullable
                      as String,
            ticketId: freezed == ticketId
                ? _value.ticketId
                : ticketId // ignore: cast_nullable_to_non_nullable
                      as String?,
            answersJson: freezed == answersJson
                ? _value.answersJson
                : answersJson // ignore: cast_nullable_to_non_nullable
                      as String?,
            joinedAt: freezed == joinedAt
                ? _value.joinedAt
                : joinedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            username: freezed == username
                ? _value.username
                : username // ignore: cast_nullable_to_non_nullable
                      as String?,
            avatarKey: freezed == avatarKey
                ? _value.avatarKey
                : avatarKey // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$GroupMemberDtoImplCopyWith<$Res>
    implements $GroupMemberDtoCopyWith<$Res> {
  factory _$$GroupMemberDtoImplCopyWith(
    _$GroupMemberDtoImpl value,
    $Res Function(_$GroupMemberDtoImpl) then,
  ) = __$$GroupMemberDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'user_id') String? userId,
    String name,
    String phone,
    @JsonKey(name: 'ticket_id') String? ticketId,
    @JsonKey(name: 'answers_json') String? answersJson,
    @JsonKey(name: 'joined_at') DateTime? joinedAt,
    String? username,
    @JsonKey(name: 'avatar_key') String? avatarKey,
  });
}

/// @nodoc
class __$$GroupMemberDtoImplCopyWithImpl<$Res>
    extends _$GroupMemberDtoCopyWithImpl<$Res, _$GroupMemberDtoImpl>
    implements _$$GroupMemberDtoImplCopyWith<$Res> {
  __$$GroupMemberDtoImplCopyWithImpl(
    _$GroupMemberDtoImpl _value,
    $Res Function(_$GroupMemberDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of GroupMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? userId = freezed,
    Object? name = null,
    Object? phone = null,
    Object? ticketId = freezed,
    Object? answersJson = freezed,
    Object? joinedAt = freezed,
    Object? username = freezed,
    Object? avatarKey = freezed,
  }) {
    return _then(
      _$GroupMemberDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        userId: freezed == userId
            ? _value.userId
            : userId // ignore: cast_nullable_to_non_nullable
                  as String?,
        name: null == name
            ? _value.name
            : name // ignore: cast_nullable_to_non_nullable
                  as String,
        phone: null == phone
            ? _value.phone
            : phone // ignore: cast_nullable_to_non_nullable
                  as String,
        ticketId: freezed == ticketId
            ? _value.ticketId
            : ticketId // ignore: cast_nullable_to_non_nullable
                  as String?,
        answersJson: freezed == answersJson
            ? _value.answersJson
            : answersJson // ignore: cast_nullable_to_non_nullable
                  as String?,
        joinedAt: freezed == joinedAt
            ? _value.joinedAt
            : joinedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        username: freezed == username
            ? _value.username
            : username // ignore: cast_nullable_to_non_nullable
                  as String?,
        avatarKey: freezed == avatarKey
            ? _value.avatarKey
            : avatarKey // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$GroupMemberDtoImpl implements _GroupMemberDto {
  const _$GroupMemberDtoImpl({
    required this.id,
    @JsonKey(name: 'user_id') this.userId,
    required this.name,
    required this.phone,
    @JsonKey(name: 'ticket_id') this.ticketId,
    @JsonKey(name: 'answers_json') this.answersJson,
    @JsonKey(name: 'joined_at') this.joinedAt,
    this.username,
    @JsonKey(name: 'avatar_key') this.avatarKey,
  });

  factory _$GroupMemberDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$GroupMemberDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'user_id')
  final String? userId;
  @override
  final String name;
  @override
  final String phone;
  @override
  @JsonKey(name: 'ticket_id')
  final String? ticketId;
  @override
  @JsonKey(name: 'answers_json')
  final String? answersJson;
  @override
  @JsonKey(name: 'joined_at')
  final DateTime? joinedAt;
  @override
  final String? username;
  @override
  @JsonKey(name: 'avatar_key')
  final String? avatarKey;

  @override
  String toString() {
    return 'GroupMemberDto(id: $id, userId: $userId, name: $name, phone: $phone, ticketId: $ticketId, answersJson: $answersJson, joinedAt: $joinedAt, username: $username, avatarKey: $avatarKey)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$GroupMemberDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.userId, userId) || other.userId == userId) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.phone, phone) || other.phone == phone) &&
            (identical(other.ticketId, ticketId) ||
                other.ticketId == ticketId) &&
            (identical(other.answersJson, answersJson) ||
                other.answersJson == answersJson) &&
            (identical(other.joinedAt, joinedAt) ||
                other.joinedAt == joinedAt) &&
            (identical(other.username, username) ||
                other.username == username) &&
            (identical(other.avatarKey, avatarKey) ||
                other.avatarKey == avatarKey));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    userId,
    name,
    phone,
    ticketId,
    answersJson,
    joinedAt,
    username,
    avatarKey,
  );

  /// Create a copy of GroupMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$GroupMemberDtoImplCopyWith<_$GroupMemberDtoImpl> get copyWith =>
      __$$GroupMemberDtoImplCopyWithImpl<_$GroupMemberDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$GroupMemberDtoImplToJson(this);
  }
}

abstract class _GroupMemberDto implements GroupMemberDto {
  const factory _GroupMemberDto({
    required final String id,
    @JsonKey(name: 'user_id') final String? userId,
    required final String name,
    required final String phone,
    @JsonKey(name: 'ticket_id') final String? ticketId,
    @JsonKey(name: 'answers_json') final String? answersJson,
    @JsonKey(name: 'joined_at') final DateTime? joinedAt,
    final String? username,
    @JsonKey(name: 'avatar_key') final String? avatarKey,
  }) = _$GroupMemberDtoImpl;

  factory _GroupMemberDto.fromJson(Map<String, dynamic> json) =
      _$GroupMemberDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'user_id')
  String? get userId;
  @override
  String get name;
  @override
  String get phone;
  @override
  @JsonKey(name: 'ticket_id')
  String? get ticketId;
  @override
  @JsonKey(name: 'answers_json')
  String? get answersJson;
  @override
  @JsonKey(name: 'joined_at')
  DateTime? get joinedAt;
  @override
  String? get username;
  @override
  @JsonKey(name: 'avatar_key')
  String? get avatarKey;

  /// Create a copy of GroupMemberDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$GroupMemberDtoImplCopyWith<_$GroupMemberDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

GroupDto _$GroupDtoFromJson(Map<String, dynamic> json) {
  return _GroupDto.fromJson(json);
}

/// @nodoc
mixin _$GroupDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'ticket_type_id')
  String get ticketTypeId => throw _privateConstructorUsedError;
  @JsonKey(name: 'group_number')
  int get groupNumber => throw _privateConstructorUsedError;
  @JsonKey(name: 'display_name')
  String? get displayName => throw _privateConstructorUsedError;
  @JsonKey(name: 'join_code')
  String get joinCode => throw _privateConstructorUsedError;
  @JsonKey(name: 'leader_user_id')
  String get leaderUserId => throw _privateConstructorUsedError;
  int get capacity => throw _privateConstructorUsedError;
  List<GroupMemberDto> get members => throw _privateConstructorUsedError;

  /// Serializes this GroupDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of GroupDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $GroupDtoCopyWith<GroupDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $GroupDtoCopyWith<$Res> {
  factory $GroupDtoCopyWith(GroupDto value, $Res Function(GroupDto) then) =
      _$GroupDtoCopyWithImpl<$Res, GroupDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String ticketTypeId,
    @JsonKey(name: 'group_number') int groupNumber,
    @JsonKey(name: 'display_name') String? displayName,
    @JsonKey(name: 'join_code') String joinCode,
    @JsonKey(name: 'leader_user_id') String leaderUserId,
    int capacity,
    List<GroupMemberDto> members,
  });
}

/// @nodoc
class _$GroupDtoCopyWithImpl<$Res, $Val extends GroupDto>
    implements $GroupDtoCopyWith<$Res> {
  _$GroupDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of GroupDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = null,
    Object? groupNumber = null,
    Object? displayName = freezed,
    Object? joinCode = null,
    Object? leaderUserId = null,
    Object? capacity = null,
    Object? members = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: null == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String,
            ticketTypeId: null == ticketTypeId
                ? _value.ticketTypeId
                : ticketTypeId // ignore: cast_nullable_to_non_nullable
                      as String,
            groupNumber: null == groupNumber
                ? _value.groupNumber
                : groupNumber // ignore: cast_nullable_to_non_nullable
                      as int,
            displayName: freezed == displayName
                ? _value.displayName
                : displayName // ignore: cast_nullable_to_non_nullable
                      as String?,
            joinCode: null == joinCode
                ? _value.joinCode
                : joinCode // ignore: cast_nullable_to_non_nullable
                      as String,
            leaderUserId: null == leaderUserId
                ? _value.leaderUserId
                : leaderUserId // ignore: cast_nullable_to_non_nullable
                      as String,
            capacity: null == capacity
                ? _value.capacity
                : capacity // ignore: cast_nullable_to_non_nullable
                      as int,
            members: null == members
                ? _value.members
                : members // ignore: cast_nullable_to_non_nullable
                      as List<GroupMemberDto>,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$GroupDtoImplCopyWith<$Res>
    implements $GroupDtoCopyWith<$Res> {
  factory _$$GroupDtoImplCopyWith(
    _$GroupDtoImpl value,
    $Res Function(_$GroupDtoImpl) then,
  ) = __$$GroupDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String ticketTypeId,
    @JsonKey(name: 'group_number') int groupNumber,
    @JsonKey(name: 'display_name') String? displayName,
    @JsonKey(name: 'join_code') String joinCode,
    @JsonKey(name: 'leader_user_id') String leaderUserId,
    int capacity,
    List<GroupMemberDto> members,
  });
}

/// @nodoc
class __$$GroupDtoImplCopyWithImpl<$Res>
    extends _$GroupDtoCopyWithImpl<$Res, _$GroupDtoImpl>
    implements _$$GroupDtoImplCopyWith<$Res> {
  __$$GroupDtoImplCopyWithImpl(
    _$GroupDtoImpl _value,
    $Res Function(_$GroupDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of GroupDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = null,
    Object? groupNumber = null,
    Object? displayName = freezed,
    Object? joinCode = null,
    Object? leaderUserId = null,
    Object? capacity = null,
    Object? members = null,
  }) {
    return _then(
      _$GroupDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        ticketTypeId: null == ticketTypeId
            ? _value.ticketTypeId
            : ticketTypeId // ignore: cast_nullable_to_non_nullable
                  as String,
        groupNumber: null == groupNumber
            ? _value.groupNumber
            : groupNumber // ignore: cast_nullable_to_non_nullable
                  as int,
        displayName: freezed == displayName
            ? _value.displayName
            : displayName // ignore: cast_nullable_to_non_nullable
                  as String?,
        joinCode: null == joinCode
            ? _value.joinCode
            : joinCode // ignore: cast_nullable_to_non_nullable
                  as String,
        leaderUserId: null == leaderUserId
            ? _value.leaderUserId
            : leaderUserId // ignore: cast_nullable_to_non_nullable
                  as String,
        capacity: null == capacity
            ? _value.capacity
            : capacity // ignore: cast_nullable_to_non_nullable
                  as int,
        members: null == members
            ? _value._members
            : members // ignore: cast_nullable_to_non_nullable
                  as List<GroupMemberDto>,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$GroupDtoImpl implements _GroupDto {
  const _$GroupDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'ticket_type_id') required this.ticketTypeId,
    @JsonKey(name: 'group_number') required this.groupNumber,
    @JsonKey(name: 'display_name') this.displayName,
    @JsonKey(name: 'join_code') required this.joinCode,
    @JsonKey(name: 'leader_user_id') required this.leaderUserId,
    required this.capacity,
    final List<GroupMemberDto> members = const [],
  }) : _members = members;

  factory _$GroupDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$GroupDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  final String ticketTypeId;
  @override
  @JsonKey(name: 'group_number')
  final int groupNumber;
  @override
  @JsonKey(name: 'display_name')
  final String? displayName;
  @override
  @JsonKey(name: 'join_code')
  final String joinCode;
  @override
  @JsonKey(name: 'leader_user_id')
  final String leaderUserId;
  @override
  final int capacity;
  final List<GroupMemberDto> _members;
  @override
  @JsonKey()
  List<GroupMemberDto> get members {
    if (_members is EqualUnmodifiableListView) return _members;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_members);
  }

  @override
  String toString() {
    return 'GroupDto(id: $id, eventId: $eventId, ticketTypeId: $ticketTypeId, groupNumber: $groupNumber, displayName: $displayName, joinCode: $joinCode, leaderUserId: $leaderUserId, capacity: $capacity, members: $members)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$GroupDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.ticketTypeId, ticketTypeId) ||
                other.ticketTypeId == ticketTypeId) &&
            (identical(other.groupNumber, groupNumber) ||
                other.groupNumber == groupNumber) &&
            (identical(other.displayName, displayName) ||
                other.displayName == displayName) &&
            (identical(other.joinCode, joinCode) ||
                other.joinCode == joinCode) &&
            (identical(other.leaderUserId, leaderUserId) ||
                other.leaderUserId == leaderUserId) &&
            (identical(other.capacity, capacity) ||
                other.capacity == capacity) &&
            const DeepCollectionEquality().equals(other._members, _members));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    ticketTypeId,
    groupNumber,
    displayName,
    joinCode,
    leaderUserId,
    capacity,
    const DeepCollectionEquality().hash(_members),
  );

  /// Create a copy of GroupDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$GroupDtoImplCopyWith<_$GroupDtoImpl> get copyWith =>
      __$$GroupDtoImplCopyWithImpl<_$GroupDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$GroupDtoImplToJson(this);
  }
}

abstract class _GroupDto implements GroupDto {
  const factory _GroupDto({
    required final String id,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'ticket_type_id') required final String ticketTypeId,
    @JsonKey(name: 'group_number') required final int groupNumber,
    @JsonKey(name: 'display_name') final String? displayName,
    @JsonKey(name: 'join_code') required final String joinCode,
    @JsonKey(name: 'leader_user_id') required final String leaderUserId,
    required final int capacity,
    final List<GroupMemberDto> members,
  }) = _$GroupDtoImpl;

  factory _GroupDto.fromJson(Map<String, dynamic> json) =
      _$GroupDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  String get ticketTypeId;
  @override
  @JsonKey(name: 'group_number')
  int get groupNumber;
  @override
  @JsonKey(name: 'display_name')
  String? get displayName;
  @override
  @JsonKey(name: 'join_code')
  String get joinCode;
  @override
  @JsonKey(name: 'leader_user_id')
  String get leaderUserId;
  @override
  int get capacity;
  @override
  List<GroupMemberDto> get members;

  /// Create a copy of GroupDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$GroupDtoImplCopyWith<_$GroupDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

TransferDto _$TransferDtoFromJson(Map<String, dynamic> json) {
  return _TransferDto.fromJson(json);
}

/// @nodoc
mixin _$TransferDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'ticket_id')
  String get ticketId => throw _privateConstructorUsedError;
  @JsonKey(name: 'to_phone')
  String get toPhone => throw _privateConstructorUsedError;
  @JsonKey(name: 'transfer_code')
  String get transferCode => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'expires_at')
  DateTime get expiresAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime get createdAt => throw _privateConstructorUsedError;

  /// Serializes this TransferDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TransferDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TransferDtoCopyWith<TransferDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TransferDtoCopyWith<$Res> {
  factory $TransferDtoCopyWith(
    TransferDto value,
    $Res Function(TransferDto) then,
  ) = _$TransferDtoCopyWithImpl<$Res, TransferDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'ticket_id') String ticketId,
    @JsonKey(name: 'to_phone') String toPhone,
    @JsonKey(name: 'transfer_code') String transferCode,
    String status,
    @JsonKey(name: 'expires_at') DateTime expiresAt,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class _$TransferDtoCopyWithImpl<$Res, $Val extends TransferDto>
    implements $TransferDtoCopyWith<$Res> {
  _$TransferDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TransferDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? ticketId = null,
    Object? toPhone = null,
    Object? transferCode = null,
    Object? status = null,
    Object? expiresAt = null,
    Object? createdAt = null,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            ticketId: null == ticketId
                ? _value.ticketId
                : ticketId // ignore: cast_nullable_to_non_nullable
                      as String,
            toPhone: null == toPhone
                ? _value.toPhone
                : toPhone // ignore: cast_nullable_to_non_nullable
                      as String,
            transferCode: null == transferCode
                ? _value.transferCode
                : transferCode // ignore: cast_nullable_to_non_nullable
                      as String,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            expiresAt: null == expiresAt
                ? _value.expiresAt
                : expiresAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
            createdAt: null == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TransferDtoImplCopyWith<$Res>
    implements $TransferDtoCopyWith<$Res> {
  factory _$$TransferDtoImplCopyWith(
    _$TransferDtoImpl value,
    $Res Function(_$TransferDtoImpl) then,
  ) = __$$TransferDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'ticket_id') String ticketId,
    @JsonKey(name: 'to_phone') String toPhone,
    @JsonKey(name: 'transfer_code') String transferCode,
    String status,
    @JsonKey(name: 'expires_at') DateTime expiresAt,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class __$$TransferDtoImplCopyWithImpl<$Res>
    extends _$TransferDtoCopyWithImpl<$Res, _$TransferDtoImpl>
    implements _$$TransferDtoImplCopyWith<$Res> {
  __$$TransferDtoImplCopyWithImpl(
    _$TransferDtoImpl _value,
    $Res Function(_$TransferDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TransferDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? ticketId = null,
    Object? toPhone = null,
    Object? transferCode = null,
    Object? status = null,
    Object? expiresAt = null,
    Object? createdAt = null,
  }) {
    return _then(
      _$TransferDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        ticketId: null == ticketId
            ? _value.ticketId
            : ticketId // ignore: cast_nullable_to_non_nullable
                  as String,
        toPhone: null == toPhone
            ? _value.toPhone
            : toPhone // ignore: cast_nullable_to_non_nullable
                  as String,
        transferCode: null == transferCode
            ? _value.transferCode
            : transferCode // ignore: cast_nullable_to_non_nullable
                  as String,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        expiresAt: null == expiresAt
            ? _value.expiresAt
            : expiresAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
        createdAt: null == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TransferDtoImpl implements _TransferDto {
  const _$TransferDtoImpl({
    required this.id,
    @JsonKey(name: 'ticket_id') required this.ticketId,
    @JsonKey(name: 'to_phone') required this.toPhone,
    @JsonKey(name: 'transfer_code') required this.transferCode,
    required this.status,
    @JsonKey(name: 'expires_at') required this.expiresAt,
    @JsonKey(name: 'created_at') required this.createdAt,
  });

  factory _$TransferDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TransferDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'ticket_id')
  final String ticketId;
  @override
  @JsonKey(name: 'to_phone')
  final String toPhone;
  @override
  @JsonKey(name: 'transfer_code')
  final String transferCode;
  @override
  final String status;
  @override
  @JsonKey(name: 'expires_at')
  final DateTime expiresAt;
  @override
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  @override
  String toString() {
    return 'TransferDto(id: $id, ticketId: $ticketId, toPhone: $toPhone, transferCode: $transferCode, status: $status, expiresAt: $expiresAt, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TransferDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.ticketId, ticketId) ||
                other.ticketId == ticketId) &&
            (identical(other.toPhone, toPhone) || other.toPhone == toPhone) &&
            (identical(other.transferCode, transferCode) ||
                other.transferCode == transferCode) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.expiresAt, expiresAt) ||
                other.expiresAt == expiresAt) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    ticketId,
    toPhone,
    transferCode,
    status,
    expiresAt,
    createdAt,
  );

  /// Create a copy of TransferDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TransferDtoImplCopyWith<_$TransferDtoImpl> get copyWith =>
      __$$TransferDtoImplCopyWithImpl<_$TransferDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$TransferDtoImplToJson(this);
  }
}

abstract class _TransferDto implements TransferDto {
  const factory _TransferDto({
    required final String id,
    @JsonKey(name: 'ticket_id') required final String ticketId,
    @JsonKey(name: 'to_phone') required final String toPhone,
    @JsonKey(name: 'transfer_code') required final String transferCode,
    required final String status,
    @JsonKey(name: 'expires_at') required final DateTime expiresAt,
    @JsonKey(name: 'created_at') required final DateTime createdAt,
  }) = _$TransferDtoImpl;

  factory _TransferDto.fromJson(Map<String, dynamic> json) =
      _$TransferDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'ticket_id')
  String get ticketId;
  @override
  @JsonKey(name: 'to_phone')
  String get toPhone;
  @override
  @JsonKey(name: 'transfer_code')
  String get transferCode;
  @override
  String get status;
  @override
  @JsonKey(name: 'expires_at')
  DateTime get expiresAt;
  @override
  @JsonKey(name: 'created_at')
  DateTime get createdAt;

  /// Create a copy of TransferDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TransferDtoImplCopyWith<_$TransferDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
