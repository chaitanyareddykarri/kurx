// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'attendee_dtos.dart';

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

  /// Lowercased server-side: `issued` / `checkedin` / `cancelled` / `transferred`.
  String get state => throw _privateConstructorUsedError;
  @JsonKey(name: 'checked_in_at')
  DateTime? get checkedInAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'answers_json')
  String? get answersJson => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

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
    @JsonKey(name: 'created_at') DateTime? createdAt,
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
    Object? createdAt = freezed,
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
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
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
    @JsonKey(name: 'created_at') DateTime? createdAt,
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
    Object? createdAt = freezed,
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
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
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
    this.state = 'issued',
    @JsonKey(name: 'checked_in_at') this.checkedInAt,
    @JsonKey(name: 'answers_json') this.answersJson,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$TicketDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$TicketDtoImplFromJson(json);

  @override
  final String id;
  @override
  final String code;

  /// Lowercased server-side: `issued` / `checkedin` / `cancelled` / `transferred`.
  @override
  @JsonKey()
  final String state;
  @override
  @JsonKey(name: 'checked_in_at')
  final DateTime? checkedInAt;
  @override
  @JsonKey(name: 'answers_json')
  final String? answersJson;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

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
    final String state,
    @JsonKey(name: 'checked_in_at') final DateTime? checkedInAt,
    @JsonKey(name: 'answers_json') final String? answersJson,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$TicketDtoImpl;

  factory _TicketDto.fromJson(Map<String, dynamic> json) =
      _$TicketDtoImpl.fromJson;

  @override
  String get id;
  @override
  String get code;

  /// Lowercased server-side: `issued` / `checkedin` / `cancelled` / `transferred`.
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
  DateTime? get createdAt;

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
  String? get ticketTypeId => throw _privateConstructorUsedError;

  /// Lowercased: `pending` / `paid` / `failed` / `refunded` / `cancelled`.
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'amount_paise')
  int get amountPaise => throw _privateConstructorUsedError;

  /// Present only on the paid path — the gateway order the client would hand to Razorpay.
  @JsonKey(name: 'razorpay_order_id')
  String? get razorpayOrderId => throw _privateConstructorUsedError;
  @JsonKey(name: 'group_id')
  String? get groupId => throw _privateConstructorUsedError;
  @JsonKey(name: 'join_code')
  String? get joinCode => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;
  List<TicketDto> get tickets => throw _privateConstructorUsedError;

  /// Returned for a guest (unauthenticated) order — possession of it is the credential (D-036).
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
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    String status,
    @JsonKey(name: 'amount_paise') int amountPaise,
    @JsonKey(name: 'razorpay_order_id') String? razorpayOrderId,
    @JsonKey(name: 'group_id') String? groupId,
    @JsonKey(name: 'join_code') String? joinCode,
    @JsonKey(name: 'created_at') DateTime? createdAt,
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
    Object? ticketTypeId = freezed,
    Object? status = null,
    Object? amountPaise = null,
    Object? razorpayOrderId = freezed,
    Object? groupId = freezed,
    Object? joinCode = freezed,
    Object? createdAt = freezed,
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
            ticketTypeId: freezed == ticketTypeId
                ? _value.ticketTypeId
                : ticketTypeId // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            amountPaise: null == amountPaise
                ? _value.amountPaise
                : amountPaise // ignore: cast_nullable_to_non_nullable
                      as int,
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
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
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
    @JsonKey(name: 'ticket_type_id') String? ticketTypeId,
    String status,
    @JsonKey(name: 'amount_paise') int amountPaise,
    @JsonKey(name: 'razorpay_order_id') String? razorpayOrderId,
    @JsonKey(name: 'group_id') String? groupId,
    @JsonKey(name: 'join_code') String? joinCode,
    @JsonKey(name: 'created_at') DateTime? createdAt,
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
    Object? ticketTypeId = freezed,
    Object? status = null,
    Object? amountPaise = null,
    Object? razorpayOrderId = freezed,
    Object? groupId = freezed,
    Object? joinCode = freezed,
    Object? createdAt = freezed,
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
        ticketTypeId: freezed == ticketTypeId
            ? _value.ticketTypeId
            : ticketTypeId // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        amountPaise: null == amountPaise
            ? _value.amountPaise
            : amountPaise // ignore: cast_nullable_to_non_nullable
                  as int,
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
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
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
class _$OrderDtoImpl extends _OrderDto {
  const _$OrderDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'ticket_type_id') this.ticketTypeId,
    this.status = 'pending',
    @JsonKey(name: 'amount_paise') this.amountPaise = 0,
    @JsonKey(name: 'razorpay_order_id') this.razorpayOrderId,
    @JsonKey(name: 'group_id') this.groupId,
    @JsonKey(name: 'join_code') this.joinCode,
    @JsonKey(name: 'created_at') this.createdAt,
    final List<TicketDto> tickets = const <TicketDto>[],
    @JsonKey(name: 'guest_access_token') this.guestAccessToken,
  }) : _tickets = tickets,
       super._();

  factory _$OrderDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$OrderDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  final String? ticketTypeId;

  /// Lowercased: `pending` / `paid` / `failed` / `refunded` / `cancelled`.
  @override
  @JsonKey()
  final String status;
  @override
  @JsonKey(name: 'amount_paise')
  final int amountPaise;

  /// Present only on the paid path — the gateway order the client would hand to Razorpay.
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
  final DateTime? createdAt;
  final List<TicketDto> _tickets;
  @override
  @JsonKey()
  List<TicketDto> get tickets {
    if (_tickets is EqualUnmodifiableListView) return _tickets;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_tickets);
  }

  /// Returned for a guest (unauthenticated) order — possession of it is the credential (D-036).
  @override
  @JsonKey(name: 'guest_access_token')
  final String? guestAccessToken;

  @override
  String toString() {
    return 'OrderDto(id: $id, eventId: $eventId, ticketTypeId: $ticketTypeId, status: $status, amountPaise: $amountPaise, razorpayOrderId: $razorpayOrderId, groupId: $groupId, joinCode: $joinCode, createdAt: $createdAt, tickets: $tickets, guestAccessToken: $guestAccessToken)';
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

abstract class _OrderDto extends OrderDto {
  const factory _OrderDto({
    required final String id,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'ticket_type_id') final String? ticketTypeId,
    final String status,
    @JsonKey(name: 'amount_paise') final int amountPaise,
    @JsonKey(name: 'razorpay_order_id') final String? razorpayOrderId,
    @JsonKey(name: 'group_id') final String? groupId,
    @JsonKey(name: 'join_code') final String? joinCode,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
    final List<TicketDto> tickets,
    @JsonKey(name: 'guest_access_token') final String? guestAccessToken,
  }) = _$OrderDtoImpl;
  const _OrderDto._() : super._();

  factory _OrderDto.fromJson(Map<String, dynamic> json) =
      _$OrderDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  String? get ticketTypeId;

  /// Lowercased: `pending` / `paid` / `failed` / `refunded` / `cancelled`.
  @override
  String get status;
  @override
  @JsonKey(name: 'amount_paise')
  int get amountPaise;

  /// Present only on the paid path — the gateway order the client would hand to Razorpay.
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
  DateTime? get createdAt;
  @override
  List<TicketDto> get tickets;

  /// Returned for a guest (unauthenticated) order — possession of it is the credential (D-036).
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

ReviewDto _$ReviewDtoFromJson(Map<String, dynamic> json) {
  return _ReviewDto.fromJson(json);
}

/// @nodoc
mixin _$ReviewDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String? get eventId => throw _privateConstructorUsedError;
  int get rating => throw _privateConstructorUsedError;
  String? get title => throw _privateConstructorUsedError;
  String? get body => throw _privateConstructorUsedError;
  @JsonKey(name: 'is_anonymous')
  bool get isAnonymous => throw _privateConstructorUsedError;

  /// The author held an issued ticket — the backend's own badge, never derived client-side.
  @JsonKey(name: 'is_verified')
  bool get isVerified => throw _privateConstructorUsedError;
  @JsonKey(name: 'author_name')
  String? get authorName => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this ReviewDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ReviewDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ReviewDtoCopyWith<ReviewDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ReviewDtoCopyWith<$Res> {
  factory $ReviewDtoCopyWith(ReviewDto value, $Res Function(ReviewDto) then) =
      _$ReviewDtoCopyWithImpl<$Res, ReviewDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String? eventId,
    int rating,
    String? title,
    String? body,
    @JsonKey(name: 'is_anonymous') bool isAnonymous,
    @JsonKey(name: 'is_verified') bool isVerified,
    @JsonKey(name: 'author_name') String? authorName,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class _$ReviewDtoCopyWithImpl<$Res, $Val extends ReviewDto>
    implements $ReviewDtoCopyWith<$Res> {
  _$ReviewDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ReviewDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = freezed,
    Object? rating = null,
    Object? title = freezed,
    Object? body = freezed,
    Object? isAnonymous = null,
    Object? isVerified = null,
    Object? authorName = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: freezed == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String?,
            rating: null == rating
                ? _value.rating
                : rating // ignore: cast_nullable_to_non_nullable
                      as int,
            title: freezed == title
                ? _value.title
                : title // ignore: cast_nullable_to_non_nullable
                      as String?,
            body: freezed == body
                ? _value.body
                : body // ignore: cast_nullable_to_non_nullable
                      as String?,
            isAnonymous: null == isAnonymous
                ? _value.isAnonymous
                : isAnonymous // ignore: cast_nullable_to_non_nullable
                      as bool,
            isVerified: null == isVerified
                ? _value.isVerified
                : isVerified // ignore: cast_nullable_to_non_nullable
                      as bool,
            authorName: freezed == authorName
                ? _value.authorName
                : authorName // ignore: cast_nullable_to_non_nullable
                      as String?,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ReviewDtoImplCopyWith<$Res>
    implements $ReviewDtoCopyWith<$Res> {
  factory _$$ReviewDtoImplCopyWith(
    _$ReviewDtoImpl value,
    $Res Function(_$ReviewDtoImpl) then,
  ) = __$$ReviewDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String? eventId,
    int rating,
    String? title,
    String? body,
    @JsonKey(name: 'is_anonymous') bool isAnonymous,
    @JsonKey(name: 'is_verified') bool isVerified,
    @JsonKey(name: 'author_name') String? authorName,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class __$$ReviewDtoImplCopyWithImpl<$Res>
    extends _$ReviewDtoCopyWithImpl<$Res, _$ReviewDtoImpl>
    implements _$$ReviewDtoImplCopyWith<$Res> {
  __$$ReviewDtoImplCopyWithImpl(
    _$ReviewDtoImpl _value,
    $Res Function(_$ReviewDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ReviewDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = freezed,
    Object? rating = null,
    Object? title = freezed,
    Object? body = freezed,
    Object? isAnonymous = null,
    Object? isVerified = null,
    Object? authorName = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$ReviewDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: freezed == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String?,
        rating: null == rating
            ? _value.rating
            : rating // ignore: cast_nullable_to_non_nullable
                  as int,
        title: freezed == title
            ? _value.title
            : title // ignore: cast_nullable_to_non_nullable
                  as String?,
        body: freezed == body
            ? _value.body
            : body // ignore: cast_nullable_to_non_nullable
                  as String?,
        isAnonymous: null == isAnonymous
            ? _value.isAnonymous
            : isAnonymous // ignore: cast_nullable_to_non_nullable
                  as bool,
        isVerified: null == isVerified
            ? _value.isVerified
            : isVerified // ignore: cast_nullable_to_non_nullable
                  as bool,
        authorName: freezed == authorName
            ? _value.authorName
            : authorName // ignore: cast_nullable_to_non_nullable
                  as String?,
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ReviewDtoImpl implements _ReviewDto {
  const _$ReviewDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') this.eventId,
    required this.rating,
    this.title,
    this.body,
    @JsonKey(name: 'is_anonymous') this.isAnonymous = false,
    @JsonKey(name: 'is_verified') this.isVerified = false,
    @JsonKey(name: 'author_name') this.authorName,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$ReviewDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ReviewDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String? eventId;
  @override
  final int rating;
  @override
  final String? title;
  @override
  final String? body;
  @override
  @JsonKey(name: 'is_anonymous')
  final bool isAnonymous;

  /// The author held an issued ticket — the backend's own badge, never derived client-side.
  @override
  @JsonKey(name: 'is_verified')
  final bool isVerified;
  @override
  @JsonKey(name: 'author_name')
  final String? authorName;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'ReviewDto(id: $id, eventId: $eventId, rating: $rating, title: $title, body: $body, isAnonymous: $isAnonymous, isVerified: $isVerified, authorName: $authorName, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ReviewDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.rating, rating) || other.rating == rating) &&
            (identical(other.title, title) || other.title == title) &&
            (identical(other.body, body) || other.body == body) &&
            (identical(other.isAnonymous, isAnonymous) ||
                other.isAnonymous == isAnonymous) &&
            (identical(other.isVerified, isVerified) ||
                other.isVerified == isVerified) &&
            (identical(other.authorName, authorName) ||
                other.authorName == authorName) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    rating,
    title,
    body,
    isAnonymous,
    isVerified,
    authorName,
    createdAt,
  );

  /// Create a copy of ReviewDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ReviewDtoImplCopyWith<_$ReviewDtoImpl> get copyWith =>
      __$$ReviewDtoImplCopyWithImpl<_$ReviewDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$ReviewDtoImplToJson(this);
  }
}

abstract class _ReviewDto implements ReviewDto {
  const factory _ReviewDto({
    required final String id,
    @JsonKey(name: 'event_id') final String? eventId,
    required final int rating,
    final String? title,
    final String? body,
    @JsonKey(name: 'is_anonymous') final bool isAnonymous,
    @JsonKey(name: 'is_verified') final bool isVerified,
    @JsonKey(name: 'author_name') final String? authorName,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$ReviewDtoImpl;

  factory _ReviewDto.fromJson(Map<String, dynamic> json) =
      _$ReviewDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String? get eventId;
  @override
  int get rating;
  @override
  String? get title;
  @override
  String? get body;
  @override
  @JsonKey(name: 'is_anonymous')
  bool get isAnonymous;

  /// The author held an issued ticket — the backend's own badge, never derived client-side.
  @override
  @JsonKey(name: 'is_verified')
  bool get isVerified;
  @override
  @JsonKey(name: 'author_name')
  String? get authorName;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of ReviewDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ReviewDtoImplCopyWith<_$ReviewDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
mixin _$ReviewPageDto {
  List<ReviewDto> get items => throw _privateConstructorUsedError;
  double get average => throw _privateConstructorUsedError;
  int get count => throw _privateConstructorUsedError;
  int get total => throw _privateConstructorUsedError;

  /// Create a copy of ReviewPageDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ReviewPageDtoCopyWith<ReviewPageDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ReviewPageDtoCopyWith<$Res> {
  factory $ReviewPageDtoCopyWith(
    ReviewPageDto value,
    $Res Function(ReviewPageDto) then,
  ) = _$ReviewPageDtoCopyWithImpl<$Res, ReviewPageDto>;
  @useResult
  $Res call({List<ReviewDto> items, double average, int count, int total});
}

/// @nodoc
class _$ReviewPageDtoCopyWithImpl<$Res, $Val extends ReviewPageDto>
    implements $ReviewPageDtoCopyWith<$Res> {
  _$ReviewPageDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ReviewPageDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? items = null,
    Object? average = null,
    Object? count = null,
    Object? total = null,
  }) {
    return _then(
      _value.copyWith(
            items: null == items
                ? _value.items
                : items // ignore: cast_nullable_to_non_nullable
                      as List<ReviewDto>,
            average: null == average
                ? _value.average
                : average // ignore: cast_nullable_to_non_nullable
                      as double,
            count: null == count
                ? _value.count
                : count // ignore: cast_nullable_to_non_nullable
                      as int,
            total: null == total
                ? _value.total
                : total // ignore: cast_nullable_to_non_nullable
                      as int,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ReviewPageDtoImplCopyWith<$Res>
    implements $ReviewPageDtoCopyWith<$Res> {
  factory _$$ReviewPageDtoImplCopyWith(
    _$ReviewPageDtoImpl value,
    $Res Function(_$ReviewPageDtoImpl) then,
  ) = __$$ReviewPageDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({List<ReviewDto> items, double average, int count, int total});
}

/// @nodoc
class __$$ReviewPageDtoImplCopyWithImpl<$Res>
    extends _$ReviewPageDtoCopyWithImpl<$Res, _$ReviewPageDtoImpl>
    implements _$$ReviewPageDtoImplCopyWith<$Res> {
  __$$ReviewPageDtoImplCopyWithImpl(
    _$ReviewPageDtoImpl _value,
    $Res Function(_$ReviewPageDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ReviewPageDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? items = null,
    Object? average = null,
    Object? count = null,
    Object? total = null,
  }) {
    return _then(
      _$ReviewPageDtoImpl(
        items: null == items
            ? _value._items
            : items // ignore: cast_nullable_to_non_nullable
                  as List<ReviewDto>,
        average: null == average
            ? _value.average
            : average // ignore: cast_nullable_to_non_nullable
                  as double,
        count: null == count
            ? _value.count
            : count // ignore: cast_nullable_to_non_nullable
                  as int,
        total: null == total
            ? _value.total
            : total // ignore: cast_nullable_to_non_nullable
                  as int,
      ),
    );
  }
}

/// @nodoc

class _$ReviewPageDtoImpl implements _ReviewPageDto {
  const _$ReviewPageDtoImpl({
    final List<ReviewDto> items = const <ReviewDto>[],
    this.average = 0,
    this.count = 0,
    this.total = 0,
  }) : _items = items;

  final List<ReviewDto> _items;
  @override
  @JsonKey()
  List<ReviewDto> get items {
    if (_items is EqualUnmodifiableListView) return _items;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_items);
  }

  @override
  @JsonKey()
  final double average;
  @override
  @JsonKey()
  final int count;
  @override
  @JsonKey()
  final int total;

  @override
  String toString() {
    return 'ReviewPageDto(items: $items, average: $average, count: $count, total: $total)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ReviewPageDtoImpl &&
            const DeepCollectionEquality().equals(other._items, _items) &&
            (identical(other.average, average) || other.average == average) &&
            (identical(other.count, count) || other.count == count) &&
            (identical(other.total, total) || other.total == total));
  }

  @override
  int get hashCode => Object.hash(
    runtimeType,
    const DeepCollectionEquality().hash(_items),
    average,
    count,
    total,
  );

  /// Create a copy of ReviewPageDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ReviewPageDtoImplCopyWith<_$ReviewPageDtoImpl> get copyWith =>
      __$$ReviewPageDtoImplCopyWithImpl<_$ReviewPageDtoImpl>(this, _$identity);
}

abstract class _ReviewPageDto implements ReviewPageDto {
  const factory _ReviewPageDto({
    final List<ReviewDto> items,
    final double average,
    final int count,
    final int total,
  }) = _$ReviewPageDtoImpl;

  @override
  List<ReviewDto> get items;
  @override
  double get average;
  @override
  int get count;
  @override
  int get total;

  /// Create a copy of ReviewPageDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ReviewPageDtoImplCopyWith<_$ReviewPageDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

WaitlistEntryDto _$WaitlistEntryDtoFromJson(Map<String, dynamic> json) {
  return _WaitlistEntryDto.fromJson(json);
}

/// @nodoc
mixin _$WaitlistEntryDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'ticket_type_id')
  String get ticketTypeId => throw _privateConstructorUsedError;
  int get position => throw _privateConstructorUsedError;

  /// Lowercased: `waiting` / `notified` / `converted` / `cancelled` / `expired`.
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'offer_expires_at')
  DateTime? get offerExpiresAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this WaitlistEntryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of WaitlistEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $WaitlistEntryDtoCopyWith<WaitlistEntryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $WaitlistEntryDtoCopyWith<$Res> {
  factory $WaitlistEntryDtoCopyWith(
    WaitlistEntryDto value,
    $Res Function(WaitlistEntryDto) then,
  ) = _$WaitlistEntryDtoCopyWithImpl<$Res, WaitlistEntryDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String ticketTypeId,
    int position,
    String status,
    @JsonKey(name: 'offer_expires_at') DateTime? offerExpiresAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class _$WaitlistEntryDtoCopyWithImpl<$Res, $Val extends WaitlistEntryDto>
    implements $WaitlistEntryDtoCopyWith<$Res> {
  _$WaitlistEntryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of WaitlistEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = null,
    Object? position = null,
    Object? status = null,
    Object? offerExpiresAt = freezed,
    Object? createdAt = freezed,
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
            position: null == position
                ? _value.position
                : position // ignore: cast_nullable_to_non_nullable
                      as int,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            offerExpiresAt: freezed == offerExpiresAt
                ? _value.offerExpiresAt
                : offerExpiresAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$WaitlistEntryDtoImplCopyWith<$Res>
    implements $WaitlistEntryDtoCopyWith<$Res> {
  factory _$$WaitlistEntryDtoImplCopyWith(
    _$WaitlistEntryDtoImpl value,
    $Res Function(_$WaitlistEntryDtoImpl) then,
  ) = __$$WaitlistEntryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'ticket_type_id') String ticketTypeId,
    int position,
    String status,
    @JsonKey(name: 'offer_expires_at') DateTime? offerExpiresAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class __$$WaitlistEntryDtoImplCopyWithImpl<$Res>
    extends _$WaitlistEntryDtoCopyWithImpl<$Res, _$WaitlistEntryDtoImpl>
    implements _$$WaitlistEntryDtoImplCopyWith<$Res> {
  __$$WaitlistEntryDtoImplCopyWithImpl(
    _$WaitlistEntryDtoImpl _value,
    $Res Function(_$WaitlistEntryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of WaitlistEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? ticketTypeId = null,
    Object? position = null,
    Object? status = null,
    Object? offerExpiresAt = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$WaitlistEntryDtoImpl(
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
        position: null == position
            ? _value.position
            : position // ignore: cast_nullable_to_non_nullable
                  as int,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        offerExpiresAt: freezed == offerExpiresAt
            ? _value.offerExpiresAt
            : offerExpiresAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$WaitlistEntryDtoImpl implements _WaitlistEntryDto {
  const _$WaitlistEntryDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'ticket_type_id') required this.ticketTypeId,
    this.position = 0,
    this.status = 'waiting',
    @JsonKey(name: 'offer_expires_at') this.offerExpiresAt,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$WaitlistEntryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$WaitlistEntryDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  final String ticketTypeId;
  @override
  @JsonKey()
  final int position;

  /// Lowercased: `waiting` / `notified` / `converted` / `cancelled` / `expired`.
  @override
  @JsonKey()
  final String status;
  @override
  @JsonKey(name: 'offer_expires_at')
  final DateTime? offerExpiresAt;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'WaitlistEntryDto(id: $id, eventId: $eventId, ticketTypeId: $ticketTypeId, position: $position, status: $status, offerExpiresAt: $offerExpiresAt, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$WaitlistEntryDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.ticketTypeId, ticketTypeId) ||
                other.ticketTypeId == ticketTypeId) &&
            (identical(other.position, position) ||
                other.position == position) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.offerExpiresAt, offerExpiresAt) ||
                other.offerExpiresAt == offerExpiresAt) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    ticketTypeId,
    position,
    status,
    offerExpiresAt,
    createdAt,
  );

  /// Create a copy of WaitlistEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$WaitlistEntryDtoImplCopyWith<_$WaitlistEntryDtoImpl> get copyWith =>
      __$$WaitlistEntryDtoImplCopyWithImpl<_$WaitlistEntryDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$WaitlistEntryDtoImplToJson(this);
  }
}

abstract class _WaitlistEntryDto implements WaitlistEntryDto {
  const factory _WaitlistEntryDto({
    required final String id,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'ticket_type_id') required final String ticketTypeId,
    final int position,
    final String status,
    @JsonKey(name: 'offer_expires_at') final DateTime? offerExpiresAt,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$WaitlistEntryDtoImpl;

  factory _WaitlistEntryDto.fromJson(Map<String, dynamic> json) =
      _$WaitlistEntryDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'ticket_type_id')
  String get ticketTypeId;
  @override
  int get position;

  /// Lowercased: `waiting` / `notified` / `converted` / `cancelled` / `expired`.
  @override
  String get status;
  @override
  @JsonKey(name: 'offer_expires_at')
  DateTime? get offerExpiresAt;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of WaitlistEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$WaitlistEntryDtoImplCopyWith<_$WaitlistEntryDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

ParticipationDto _$ParticipationDtoFromJson(Map<String, dynamic> json) {
  return _ParticipationDto.fromJson(json);
}

/// @nodoc
mixin _$ParticipationDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'subject_type')
  String? get subjectType => throw _privateConstructorUsedError;
  @JsonKey(name: 'subject_id')
  String? get subjectId => throw _privateConstructorUsedError;
  @JsonKey(name: 'role_slug')
  String get roleSlug => throw _privateConstructorUsedError;
  @JsonKey(name: 'role_class')
  String? get roleClass => throw _privateConstructorUsedError;
  @JsonKey(name: 'custom_label')
  String? get customLabel => throw _privateConstructorUsedError;

  /// `invited` / `accepted` / `declined` / `removed`.
  String get state => throw _privateConstructorUsedError;
  String? get visibility => throw _privateConstructorUsedError;
  @JsonKey(name: 'counts_toward_capacity')
  bool get countsTowardCapacity => throw _privateConstructorUsedError;
  @JsonKey(name: 'inventory_segment')
  String? get inventorySegment => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this ParticipationDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ParticipationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ParticipationDtoCopyWith<ParticipationDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ParticipationDtoCopyWith<$Res> {
  factory $ParticipationDtoCopyWith(
    ParticipationDto value,
    $Res Function(ParticipationDto) then,
  ) = _$ParticipationDtoCopyWithImpl<$Res, ParticipationDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'subject_type') String? subjectType,
    @JsonKey(name: 'subject_id') String? subjectId,
    @JsonKey(name: 'role_slug') String roleSlug,
    @JsonKey(name: 'role_class') String? roleClass,
    @JsonKey(name: 'custom_label') String? customLabel,
    String state,
    String? visibility,
    @JsonKey(name: 'counts_toward_capacity') bool countsTowardCapacity,
    @JsonKey(name: 'inventory_segment') String? inventorySegment,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class _$ParticipationDtoCopyWithImpl<$Res, $Val extends ParticipationDto>
    implements $ParticipationDtoCopyWith<$Res> {
  _$ParticipationDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ParticipationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? subjectType = freezed,
    Object? subjectId = freezed,
    Object? roleSlug = null,
    Object? roleClass = freezed,
    Object? customLabel = freezed,
    Object? state = null,
    Object? visibility = freezed,
    Object? countsTowardCapacity = null,
    Object? inventorySegment = freezed,
    Object? createdAt = freezed,
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
            subjectType: freezed == subjectType
                ? _value.subjectType
                : subjectType // ignore: cast_nullable_to_non_nullable
                      as String?,
            subjectId: freezed == subjectId
                ? _value.subjectId
                : subjectId // ignore: cast_nullable_to_non_nullable
                      as String?,
            roleSlug: null == roleSlug
                ? _value.roleSlug
                : roleSlug // ignore: cast_nullable_to_non_nullable
                      as String,
            roleClass: freezed == roleClass
                ? _value.roleClass
                : roleClass // ignore: cast_nullable_to_non_nullable
                      as String?,
            customLabel: freezed == customLabel
                ? _value.customLabel
                : customLabel // ignore: cast_nullable_to_non_nullable
                      as String?,
            state: null == state
                ? _value.state
                : state // ignore: cast_nullable_to_non_nullable
                      as String,
            visibility: freezed == visibility
                ? _value.visibility
                : visibility // ignore: cast_nullable_to_non_nullable
                      as String?,
            countsTowardCapacity: null == countsTowardCapacity
                ? _value.countsTowardCapacity
                : countsTowardCapacity // ignore: cast_nullable_to_non_nullable
                      as bool,
            inventorySegment: freezed == inventorySegment
                ? _value.inventorySegment
                : inventorySegment // ignore: cast_nullable_to_non_nullable
                      as String?,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$ParticipationDtoImplCopyWith<$Res>
    implements $ParticipationDtoCopyWith<$Res> {
  factory _$$ParticipationDtoImplCopyWith(
    _$ParticipationDtoImpl value,
    $Res Function(_$ParticipationDtoImpl) then,
  ) = __$$ParticipationDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'event_id') String eventId,
    @JsonKey(name: 'subject_type') String? subjectType,
    @JsonKey(name: 'subject_id') String? subjectId,
    @JsonKey(name: 'role_slug') String roleSlug,
    @JsonKey(name: 'role_class') String? roleClass,
    @JsonKey(name: 'custom_label') String? customLabel,
    String state,
    String? visibility,
    @JsonKey(name: 'counts_toward_capacity') bool countsTowardCapacity,
    @JsonKey(name: 'inventory_segment') String? inventorySegment,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class __$$ParticipationDtoImplCopyWithImpl<$Res>
    extends _$ParticipationDtoCopyWithImpl<$Res, _$ParticipationDtoImpl>
    implements _$$ParticipationDtoImplCopyWith<$Res> {
  __$$ParticipationDtoImplCopyWithImpl(
    _$ParticipationDtoImpl _value,
    $Res Function(_$ParticipationDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of ParticipationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? eventId = null,
    Object? subjectType = freezed,
    Object? subjectId = freezed,
    Object? roleSlug = null,
    Object? roleClass = freezed,
    Object? customLabel = freezed,
    Object? state = null,
    Object? visibility = freezed,
    Object? countsTowardCapacity = null,
    Object? inventorySegment = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$ParticipationDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: null == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String,
        subjectType: freezed == subjectType
            ? _value.subjectType
            : subjectType // ignore: cast_nullable_to_non_nullable
                  as String?,
        subjectId: freezed == subjectId
            ? _value.subjectId
            : subjectId // ignore: cast_nullable_to_non_nullable
                  as String?,
        roleSlug: null == roleSlug
            ? _value.roleSlug
            : roleSlug // ignore: cast_nullable_to_non_nullable
                  as String,
        roleClass: freezed == roleClass
            ? _value.roleClass
            : roleClass // ignore: cast_nullable_to_non_nullable
                  as String?,
        customLabel: freezed == customLabel
            ? _value.customLabel
            : customLabel // ignore: cast_nullable_to_non_nullable
                  as String?,
        state: null == state
            ? _value.state
            : state // ignore: cast_nullable_to_non_nullable
                  as String,
        visibility: freezed == visibility
            ? _value.visibility
            : visibility // ignore: cast_nullable_to_non_nullable
                  as String?,
        countsTowardCapacity: null == countsTowardCapacity
            ? _value.countsTowardCapacity
            : countsTowardCapacity // ignore: cast_nullable_to_non_nullable
                  as bool,
        inventorySegment: freezed == inventorySegment
            ? _value.inventorySegment
            : inventorySegment // ignore: cast_nullable_to_non_nullable
                  as String?,
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$ParticipationDtoImpl implements _ParticipationDto {
  const _$ParticipationDtoImpl({
    required this.id,
    @JsonKey(name: 'event_id') required this.eventId,
    @JsonKey(name: 'subject_type') this.subjectType,
    @JsonKey(name: 'subject_id') this.subjectId,
    @JsonKey(name: 'role_slug') required this.roleSlug,
    @JsonKey(name: 'role_class') this.roleClass,
    @JsonKey(name: 'custom_label') this.customLabel,
    this.state = 'invited',
    this.visibility,
    @JsonKey(name: 'counts_toward_capacity') this.countsTowardCapacity = false,
    @JsonKey(name: 'inventory_segment') this.inventorySegment,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$ParticipationDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$ParticipationDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'event_id')
  final String eventId;
  @override
  @JsonKey(name: 'subject_type')
  final String? subjectType;
  @override
  @JsonKey(name: 'subject_id')
  final String? subjectId;
  @override
  @JsonKey(name: 'role_slug')
  final String roleSlug;
  @override
  @JsonKey(name: 'role_class')
  final String? roleClass;
  @override
  @JsonKey(name: 'custom_label')
  final String? customLabel;

  /// `invited` / `accepted` / `declined` / `removed`.
  @override
  @JsonKey()
  final String state;
  @override
  final String? visibility;
  @override
  @JsonKey(name: 'counts_toward_capacity')
  final bool countsTowardCapacity;
  @override
  @JsonKey(name: 'inventory_segment')
  final String? inventorySegment;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'ParticipationDto(id: $id, eventId: $eventId, subjectType: $subjectType, subjectId: $subjectId, roleSlug: $roleSlug, roleClass: $roleClass, customLabel: $customLabel, state: $state, visibility: $visibility, countsTowardCapacity: $countsTowardCapacity, inventorySegment: $inventorySegment, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ParticipationDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.subjectType, subjectType) ||
                other.subjectType == subjectType) &&
            (identical(other.subjectId, subjectId) ||
                other.subjectId == subjectId) &&
            (identical(other.roleSlug, roleSlug) ||
                other.roleSlug == roleSlug) &&
            (identical(other.roleClass, roleClass) ||
                other.roleClass == roleClass) &&
            (identical(other.customLabel, customLabel) ||
                other.customLabel == customLabel) &&
            (identical(other.state, state) || other.state == state) &&
            (identical(other.visibility, visibility) ||
                other.visibility == visibility) &&
            (identical(other.countsTowardCapacity, countsTowardCapacity) ||
                other.countsTowardCapacity == countsTowardCapacity) &&
            (identical(other.inventorySegment, inventorySegment) ||
                other.inventorySegment == inventorySegment) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    eventId,
    subjectType,
    subjectId,
    roleSlug,
    roleClass,
    customLabel,
    state,
    visibility,
    countsTowardCapacity,
    inventorySegment,
    createdAt,
  );

  /// Create a copy of ParticipationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ParticipationDtoImplCopyWith<_$ParticipationDtoImpl> get copyWith =>
      __$$ParticipationDtoImplCopyWithImpl<_$ParticipationDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$ParticipationDtoImplToJson(this);
  }
}

abstract class _ParticipationDto implements ParticipationDto {
  const factory _ParticipationDto({
    required final String id,
    @JsonKey(name: 'event_id') required final String eventId,
    @JsonKey(name: 'subject_type') final String? subjectType,
    @JsonKey(name: 'subject_id') final String? subjectId,
    @JsonKey(name: 'role_slug') required final String roleSlug,
    @JsonKey(name: 'role_class') final String? roleClass,
    @JsonKey(name: 'custom_label') final String? customLabel,
    final String state,
    final String? visibility,
    @JsonKey(name: 'counts_toward_capacity') final bool countsTowardCapacity,
    @JsonKey(name: 'inventory_segment') final String? inventorySegment,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$ParticipationDtoImpl;

  factory _ParticipationDto.fromJson(Map<String, dynamic> json) =
      _$ParticipationDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'event_id')
  String get eventId;
  @override
  @JsonKey(name: 'subject_type')
  String? get subjectType;
  @override
  @JsonKey(name: 'subject_id')
  String? get subjectId;
  @override
  @JsonKey(name: 'role_slug')
  String get roleSlug;
  @override
  @JsonKey(name: 'role_class')
  String? get roleClass;
  @override
  @JsonKey(name: 'custom_label')
  String? get customLabel;

  /// `invited` / `accepted` / `declined` / `removed`.
  @override
  String get state;
  @override
  String? get visibility;
  @override
  @JsonKey(name: 'counts_toward_capacity')
  bool get countsTowardCapacity;
  @override
  @JsonKey(name: 'inventory_segment')
  String? get inventorySegment;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of ParticipationDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ParticipationDtoImplCopyWith<_$ParticipationDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

OrgInvitationDto _$OrgInvitationDtoFromJson(Map<String, dynamic> json) {
  return _OrgInvitationDto.fromJson(json);
}

/// @nodoc
mixin _$OrgInvitationDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_id')
  String get orgId => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_name')
  String? get orgName => throw _privateConstructorUsedError;
  @JsonKey(name: 'invited_phone')
  String? get invitedPhone => throw _privateConstructorUsedError;
  String get role => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'expires_at')
  DateTime? get expiresAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this OrgInvitationDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of OrgInvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $OrgInvitationDtoCopyWith<OrgInvitationDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $OrgInvitationDtoCopyWith<$Res> {
  factory $OrgInvitationDtoCopyWith(
    OrgInvitationDto value,
    $Res Function(OrgInvitationDto) then,
  ) = _$OrgInvitationDtoCopyWithImpl<$Res, OrgInvitationDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'org_name') String? orgName,
    @JsonKey(name: 'invited_phone') String? invitedPhone,
    String role,
    String status,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class _$OrgInvitationDtoCopyWithImpl<$Res, $Val extends OrgInvitationDto>
    implements $OrgInvitationDtoCopyWith<$Res> {
  _$OrgInvitationDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of OrgInvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = null,
    Object? orgName = freezed,
    Object? invitedPhone = freezed,
    Object? role = null,
    Object? status = null,
    Object? expiresAt = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            orgId: null == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String,
            orgName: freezed == orgName
                ? _value.orgName
                : orgName // ignore: cast_nullable_to_non_nullable
                      as String?,
            invitedPhone: freezed == invitedPhone
                ? _value.invitedPhone
                : invitedPhone // ignore: cast_nullable_to_non_nullable
                      as String?,
            role: null == role
                ? _value.role
                : role // ignore: cast_nullable_to_non_nullable
                      as String,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            expiresAt: freezed == expiresAt
                ? _value.expiresAt
                : expiresAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$OrgInvitationDtoImplCopyWith<$Res>
    implements $OrgInvitationDtoCopyWith<$Res> {
  factory _$$OrgInvitationDtoImplCopyWith(
    _$OrgInvitationDtoImpl value,
    $Res Function(_$OrgInvitationDtoImpl) then,
  ) = __$$OrgInvitationDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'org_id') String orgId,
    @JsonKey(name: 'org_name') String? orgName,
    @JsonKey(name: 'invited_phone') String? invitedPhone,
    String role,
    String status,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class __$$OrgInvitationDtoImplCopyWithImpl<$Res>
    extends _$OrgInvitationDtoCopyWithImpl<$Res, _$OrgInvitationDtoImpl>
    implements _$$OrgInvitationDtoImplCopyWith<$Res> {
  __$$OrgInvitationDtoImplCopyWithImpl(
    _$OrgInvitationDtoImpl _value,
    $Res Function(_$OrgInvitationDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of OrgInvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orgId = null,
    Object? orgName = freezed,
    Object? invitedPhone = freezed,
    Object? role = null,
    Object? status = null,
    Object? expiresAt = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$OrgInvitationDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        orgId: null == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String,
        orgName: freezed == orgName
            ? _value.orgName
            : orgName // ignore: cast_nullable_to_non_nullable
                  as String?,
        invitedPhone: freezed == invitedPhone
            ? _value.invitedPhone
            : invitedPhone // ignore: cast_nullable_to_non_nullable
                  as String?,
        role: null == role
            ? _value.role
            : role // ignore: cast_nullable_to_non_nullable
                  as String,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        expiresAt: freezed == expiresAt
            ? _value.expiresAt
            : expiresAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$OrgInvitationDtoImpl implements _OrgInvitationDto {
  const _$OrgInvitationDtoImpl({
    required this.id,
    @JsonKey(name: 'org_id') required this.orgId,
    @JsonKey(name: 'org_name') this.orgName,
    @JsonKey(name: 'invited_phone') this.invitedPhone,
    this.role = 'staff',
    this.status = 'pending',
    @JsonKey(name: 'expires_at') this.expiresAt,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$OrgInvitationDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$OrgInvitationDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'org_id')
  final String orgId;
  @override
  @JsonKey(name: 'org_name')
  final String? orgName;
  @override
  @JsonKey(name: 'invited_phone')
  final String? invitedPhone;
  @override
  @JsonKey()
  final String role;
  @override
  @JsonKey()
  final String status;
  @override
  @JsonKey(name: 'expires_at')
  final DateTime? expiresAt;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'OrgInvitationDto(id: $id, orgId: $orgId, orgName: $orgName, invitedPhone: $invitedPhone, role: $role, status: $status, expiresAt: $expiresAt, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$OrgInvitationDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.orgName, orgName) || other.orgName == orgName) &&
            (identical(other.invitedPhone, invitedPhone) ||
                other.invitedPhone == invitedPhone) &&
            (identical(other.role, role) || other.role == role) &&
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
    orgId,
    orgName,
    invitedPhone,
    role,
    status,
    expiresAt,
    createdAt,
  );

  /// Create a copy of OrgInvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$OrgInvitationDtoImplCopyWith<_$OrgInvitationDtoImpl> get copyWith =>
      __$$OrgInvitationDtoImplCopyWithImpl<_$OrgInvitationDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$OrgInvitationDtoImplToJson(this);
  }
}

abstract class _OrgInvitationDto implements OrgInvitationDto {
  const factory _OrgInvitationDto({
    required final String id,
    @JsonKey(name: 'org_id') required final String orgId,
    @JsonKey(name: 'org_name') final String? orgName,
    @JsonKey(name: 'invited_phone') final String? invitedPhone,
    final String role,
    final String status,
    @JsonKey(name: 'expires_at') final DateTime? expiresAt,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$OrgInvitationDtoImpl;

  factory _OrgInvitationDto.fromJson(Map<String, dynamic> json) =
      _$OrgInvitationDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'org_id')
  String get orgId;
  @override
  @JsonKey(name: 'org_name')
  String? get orgName;
  @override
  @JsonKey(name: 'invited_phone')
  String? get invitedPhone;
  @override
  String get role;
  @override
  String get status;
  @override
  @JsonKey(name: 'expires_at')
  DateTime? get expiresAt;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of OrgInvitationDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$OrgInvitationDtoImplCopyWith<_$OrgInvitationDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

EligibilityDto _$EligibilityDtoFromJson(Map<String, dynamic> json) {
  return _EligibilityDto.fromJson(json);
}

/// @nodoc
mixin _$EligibilityDto {
  bool get allowed => throw _privateConstructorUsedError;
  String? get reason => throw _privateConstructorUsedError;

  /// Serializes this EligibilityDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of EligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $EligibilityDtoCopyWith<EligibilityDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $EligibilityDtoCopyWith<$Res> {
  factory $EligibilityDtoCopyWith(
    EligibilityDto value,
    $Res Function(EligibilityDto) then,
  ) = _$EligibilityDtoCopyWithImpl<$Res, EligibilityDto>;
  @useResult
  $Res call({bool allowed, String? reason});
}

/// @nodoc
class _$EligibilityDtoCopyWithImpl<$Res, $Val extends EligibilityDto>
    implements $EligibilityDtoCopyWith<$Res> {
  _$EligibilityDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of EligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? allowed = null, Object? reason = freezed}) {
    return _then(
      _value.copyWith(
            allowed: null == allowed
                ? _value.allowed
                : allowed // ignore: cast_nullable_to_non_nullable
                      as bool,
            reason: freezed == reason
                ? _value.reason
                : reason // ignore: cast_nullable_to_non_nullable
                      as String?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$EligibilityDtoImplCopyWith<$Res>
    implements $EligibilityDtoCopyWith<$Res> {
  factory _$$EligibilityDtoImplCopyWith(
    _$EligibilityDtoImpl value,
    $Res Function(_$EligibilityDtoImpl) then,
  ) = __$$EligibilityDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({bool allowed, String? reason});
}

/// @nodoc
class __$$EligibilityDtoImplCopyWithImpl<$Res>
    extends _$EligibilityDtoCopyWithImpl<$Res, _$EligibilityDtoImpl>
    implements _$$EligibilityDtoImplCopyWith<$Res> {
  __$$EligibilityDtoImplCopyWithImpl(
    _$EligibilityDtoImpl _value,
    $Res Function(_$EligibilityDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of EligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({Object? allowed = null, Object? reason = freezed}) {
    return _then(
      _$EligibilityDtoImpl(
        allowed: null == allowed
            ? _value.allowed
            : allowed // ignore: cast_nullable_to_non_nullable
                  as bool,
        reason: freezed == reason
            ? _value.reason
            : reason // ignore: cast_nullable_to_non_nullable
                  as String?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$EligibilityDtoImpl implements _EligibilityDto {
  const _$EligibilityDtoImpl({this.allowed = true, this.reason});

  factory _$EligibilityDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$EligibilityDtoImplFromJson(json);

  @override
  @JsonKey()
  final bool allowed;
  @override
  final String? reason;

  @override
  String toString() {
    return 'EligibilityDto(allowed: $allowed, reason: $reason)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$EligibilityDtoImpl &&
            (identical(other.allowed, allowed) || other.allowed == allowed) &&
            (identical(other.reason, reason) || other.reason == reason));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, allowed, reason);

  /// Create a copy of EligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$EligibilityDtoImplCopyWith<_$EligibilityDtoImpl> get copyWith =>
      __$$EligibilityDtoImplCopyWithImpl<_$EligibilityDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$EligibilityDtoImplToJson(this);
  }
}

abstract class _EligibilityDto implements EligibilityDto {
  const factory _EligibilityDto({final bool allowed, final String? reason}) =
      _$EligibilityDtoImpl;

  factory _EligibilityDto.fromJson(Map<String, dynamic> json) =
      _$EligibilityDtoImpl.fromJson;

  @override
  bool get allowed;
  @override
  String? get reason;

  /// Create a copy of EligibilityDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$EligibilityDtoImplCopyWith<_$EligibilityDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

IdentityHistoryEntryDto _$IdentityHistoryEntryDtoFromJson(
  Map<String, dynamic> json,
) {
  return _IdentityHistoryEntryDto.fromJson(json);
}

/// @nodoc
mixin _$IdentityHistoryEntryDto {
  String get component => throw _privateConstructorUsedError;
  String get decision => throw _privateConstructorUsedError;
  @JsonKey(name: 'reviewer_id')
  String? get reviewerId => throw _privateConstructorUsedError;
  @JsonKey(name: 'reason_code')
  String? get reasonCode => throw _privateConstructorUsedError;
  String? get notes => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime get createdAt => throw _privateConstructorUsedError;

  /// Serializes this IdentityHistoryEntryDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of IdentityHistoryEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $IdentityHistoryEntryDtoCopyWith<IdentityHistoryEntryDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $IdentityHistoryEntryDtoCopyWith<$Res> {
  factory $IdentityHistoryEntryDtoCopyWith(
    IdentityHistoryEntryDto value,
    $Res Function(IdentityHistoryEntryDto) then,
  ) = _$IdentityHistoryEntryDtoCopyWithImpl<$Res, IdentityHistoryEntryDto>;
  @useResult
  $Res call({
    String component,
    String decision,
    @JsonKey(name: 'reviewer_id') String? reviewerId,
    @JsonKey(name: 'reason_code') String? reasonCode,
    String? notes,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class _$IdentityHistoryEntryDtoCopyWithImpl<
  $Res,
  $Val extends IdentityHistoryEntryDto
>
    implements $IdentityHistoryEntryDtoCopyWith<$Res> {
  _$IdentityHistoryEntryDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of IdentityHistoryEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? component = null,
    Object? decision = null,
    Object? reviewerId = freezed,
    Object? reasonCode = freezed,
    Object? notes = freezed,
    Object? createdAt = null,
  }) {
    return _then(
      _value.copyWith(
            component: null == component
                ? _value.component
                : component // ignore: cast_nullable_to_non_nullable
                      as String,
            decision: null == decision
                ? _value.decision
                : decision // ignore: cast_nullable_to_non_nullable
                      as String,
            reviewerId: freezed == reviewerId
                ? _value.reviewerId
                : reviewerId // ignore: cast_nullable_to_non_nullable
                      as String?,
            reasonCode: freezed == reasonCode
                ? _value.reasonCode
                : reasonCode // ignore: cast_nullable_to_non_nullable
                      as String?,
            notes: freezed == notes
                ? _value.notes
                : notes // ignore: cast_nullable_to_non_nullable
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
abstract class _$$IdentityHistoryEntryDtoImplCopyWith<$Res>
    implements $IdentityHistoryEntryDtoCopyWith<$Res> {
  factory _$$IdentityHistoryEntryDtoImplCopyWith(
    _$IdentityHistoryEntryDtoImpl value,
    $Res Function(_$IdentityHistoryEntryDtoImpl) then,
  ) = __$$IdentityHistoryEntryDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String component,
    String decision,
    @JsonKey(name: 'reviewer_id') String? reviewerId,
    @JsonKey(name: 'reason_code') String? reasonCode,
    String? notes,
    @JsonKey(name: 'created_at') DateTime createdAt,
  });
}

/// @nodoc
class __$$IdentityHistoryEntryDtoImplCopyWithImpl<$Res>
    extends
        _$IdentityHistoryEntryDtoCopyWithImpl<
          $Res,
          _$IdentityHistoryEntryDtoImpl
        >
    implements _$$IdentityHistoryEntryDtoImplCopyWith<$Res> {
  __$$IdentityHistoryEntryDtoImplCopyWithImpl(
    _$IdentityHistoryEntryDtoImpl _value,
    $Res Function(_$IdentityHistoryEntryDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of IdentityHistoryEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? component = null,
    Object? decision = null,
    Object? reviewerId = freezed,
    Object? reasonCode = freezed,
    Object? notes = freezed,
    Object? createdAt = null,
  }) {
    return _then(
      _$IdentityHistoryEntryDtoImpl(
        component: null == component
            ? _value.component
            : component // ignore: cast_nullable_to_non_nullable
                  as String,
        decision: null == decision
            ? _value.decision
            : decision // ignore: cast_nullable_to_non_nullable
                  as String,
        reviewerId: freezed == reviewerId
            ? _value.reviewerId
            : reviewerId // ignore: cast_nullable_to_non_nullable
                  as String?,
        reasonCode: freezed == reasonCode
            ? _value.reasonCode
            : reasonCode // ignore: cast_nullable_to_non_nullable
                  as String?,
        notes: freezed == notes
            ? _value.notes
            : notes // ignore: cast_nullable_to_non_nullable
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
class _$IdentityHistoryEntryDtoImpl implements _IdentityHistoryEntryDto {
  const _$IdentityHistoryEntryDtoImpl({
    required this.component,
    required this.decision,
    @JsonKey(name: 'reviewer_id') this.reviewerId,
    @JsonKey(name: 'reason_code') this.reasonCode,
    this.notes,
    @JsonKey(name: 'created_at') required this.createdAt,
  });

  factory _$IdentityHistoryEntryDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$IdentityHistoryEntryDtoImplFromJson(json);

  @override
  final String component;
  @override
  final String decision;
  @override
  @JsonKey(name: 'reviewer_id')
  final String? reviewerId;
  @override
  @JsonKey(name: 'reason_code')
  final String? reasonCode;
  @override
  final String? notes;
  @override
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  @override
  String toString() {
    return 'IdentityHistoryEntryDto(component: $component, decision: $decision, reviewerId: $reviewerId, reasonCode: $reasonCode, notes: $notes, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$IdentityHistoryEntryDtoImpl &&
            (identical(other.component, component) ||
                other.component == component) &&
            (identical(other.decision, decision) ||
                other.decision == decision) &&
            (identical(other.reviewerId, reviewerId) ||
                other.reviewerId == reviewerId) &&
            (identical(other.reasonCode, reasonCode) ||
                other.reasonCode == reasonCode) &&
            (identical(other.notes, notes) || other.notes == notes) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    component,
    decision,
    reviewerId,
    reasonCode,
    notes,
    createdAt,
  );

  /// Create a copy of IdentityHistoryEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$IdentityHistoryEntryDtoImplCopyWith<_$IdentityHistoryEntryDtoImpl>
  get copyWith =>
      __$$IdentityHistoryEntryDtoImplCopyWithImpl<
        _$IdentityHistoryEntryDtoImpl
      >(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$IdentityHistoryEntryDtoImplToJson(this);
  }
}

abstract class _IdentityHistoryEntryDto implements IdentityHistoryEntryDto {
  const factory _IdentityHistoryEntryDto({
    required final String component,
    required final String decision,
    @JsonKey(name: 'reviewer_id') final String? reviewerId,
    @JsonKey(name: 'reason_code') final String? reasonCode,
    final String? notes,
    @JsonKey(name: 'created_at') required final DateTime createdAt,
  }) = _$IdentityHistoryEntryDtoImpl;

  factory _IdentityHistoryEntryDto.fromJson(Map<String, dynamic> json) =
      _$IdentityHistoryEntryDtoImpl.fromJson;

  @override
  String get component;
  @override
  String get decision;
  @override
  @JsonKey(name: 'reviewer_id')
  String? get reviewerId;
  @override
  @JsonKey(name: 'reason_code')
  String? get reasonCode;
  @override
  String? get notes;
  @override
  @JsonKey(name: 'created_at')
  DateTime get createdAt;

  /// Create a copy of IdentityHistoryEntryDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$IdentityHistoryEntryDtoImplCopyWith<_$IdentityHistoryEntryDtoImpl>
  get copyWith => throw _privateConstructorUsedError;
}

IdentityStatusDto _$IdentityStatusDtoFromJson(Map<String, dynamic> json) {
  return _IdentityStatusDto.fromJson(json);
}

/// @nodoc
mixin _$IdentityStatusDto {
  /// L0–L5 trust label.
  String? get level => throw _privateConstructorUsedError;

  /// `unverified` / `pending` / `verified` / `rejected`.
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'govt_id_kind')
  String? get govtIdKind => throw _privateConstructorUsedError;
  @JsonKey(name: 'govt_id_last4')
  String? get govtIdLast4 => throw _privateConstructorUsedError;
  @JsonKey(name: 'pan_last4')
  String? get panLast4 => throw _privateConstructorUsedError;
  @JsonKey(name: 'bank_last4')
  String? get bankLast4 => throw _privateConstructorUsedError; // Per-component state. The aggregate `status` above cannot answer "is my PAN verified" — every
  // submission wrote it, so one failed bank check reported the whole identity rejected. Defaulted
  // so a client pinned to an older backend still parses.
  @JsonKey(name: 'govt_id_status')
  String get govtIdStatus => throw _privateConstructorUsedError;
  @JsonKey(name: 'pan_status')
  String get panStatus => throw _privateConstructorUsedError;
  @JsonKey(name: 'bank_status')
  String get bankStatus => throw _privateConstructorUsedError;

  /// Penny drop proves the account exists and accepts deposits; `bankLast4` alone only ever meant
  /// a number was typed.
  @JsonKey(name: 'penny_drop_status')
  String get pennyDropStatus => throw _privateConstructorUsedError;

  /// Whether the bank's registered holder name matched — the control that catches a PAN and a bank
  /// account belonging to two different people.
  @JsonKey(name: 'bank_name_match')
  String get bankNameMatch => throw _privateConstructorUsedError;
  @JsonKey(name: 'bank_verified_at')
  DateTime? get bankVerifiedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'reviewed_at')
  DateTime? get reviewedAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'expires_at')
  DateTime? get expiresAt => throw _privateConstructorUsedError;
  @JsonKey(name: 'updated_at')
  DateTime? get updatedAt => throw _privateConstructorUsedError;

  /// Serializes this IdentityStatusDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of IdentityStatusDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $IdentityStatusDtoCopyWith<IdentityStatusDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $IdentityStatusDtoCopyWith<$Res> {
  factory $IdentityStatusDtoCopyWith(
    IdentityStatusDto value,
    $Res Function(IdentityStatusDto) then,
  ) = _$IdentityStatusDtoCopyWithImpl<$Res, IdentityStatusDto>;
  @useResult
  $Res call({
    String? level,
    String status,
    @JsonKey(name: 'govt_id_kind') String? govtIdKind,
    @JsonKey(name: 'govt_id_last4') String? govtIdLast4,
    @JsonKey(name: 'pan_last4') String? panLast4,
    @JsonKey(name: 'bank_last4') String? bankLast4,
    @JsonKey(name: 'govt_id_status') String govtIdStatus,
    @JsonKey(name: 'pan_status') String panStatus,
    @JsonKey(name: 'bank_status') String bankStatus,
    @JsonKey(name: 'penny_drop_status') String pennyDropStatus,
    @JsonKey(name: 'bank_name_match') String bankNameMatch,
    @JsonKey(name: 'bank_verified_at') DateTime? bankVerifiedAt,
    @JsonKey(name: 'reviewed_at') DateTime? reviewedAt,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
    @JsonKey(name: 'updated_at') DateTime? updatedAt,
  });
}

/// @nodoc
class _$IdentityStatusDtoCopyWithImpl<$Res, $Val extends IdentityStatusDto>
    implements $IdentityStatusDtoCopyWith<$Res> {
  _$IdentityStatusDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of IdentityStatusDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? level = freezed,
    Object? status = null,
    Object? govtIdKind = freezed,
    Object? govtIdLast4 = freezed,
    Object? panLast4 = freezed,
    Object? bankLast4 = freezed,
    Object? govtIdStatus = null,
    Object? panStatus = null,
    Object? bankStatus = null,
    Object? pennyDropStatus = null,
    Object? bankNameMatch = null,
    Object? bankVerifiedAt = freezed,
    Object? reviewedAt = freezed,
    Object? expiresAt = freezed,
    Object? updatedAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            level: freezed == level
                ? _value.level
                : level // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            govtIdKind: freezed == govtIdKind
                ? _value.govtIdKind
                : govtIdKind // ignore: cast_nullable_to_non_nullable
                      as String?,
            govtIdLast4: freezed == govtIdLast4
                ? _value.govtIdLast4
                : govtIdLast4 // ignore: cast_nullable_to_non_nullable
                      as String?,
            panLast4: freezed == panLast4
                ? _value.panLast4
                : panLast4 // ignore: cast_nullable_to_non_nullable
                      as String?,
            bankLast4: freezed == bankLast4
                ? _value.bankLast4
                : bankLast4 // ignore: cast_nullable_to_non_nullable
                      as String?,
            govtIdStatus: null == govtIdStatus
                ? _value.govtIdStatus
                : govtIdStatus // ignore: cast_nullable_to_non_nullable
                      as String,
            panStatus: null == panStatus
                ? _value.panStatus
                : panStatus // ignore: cast_nullable_to_non_nullable
                      as String,
            bankStatus: null == bankStatus
                ? _value.bankStatus
                : bankStatus // ignore: cast_nullable_to_non_nullable
                      as String,
            pennyDropStatus: null == pennyDropStatus
                ? _value.pennyDropStatus
                : pennyDropStatus // ignore: cast_nullable_to_non_nullable
                      as String,
            bankNameMatch: null == bankNameMatch
                ? _value.bankNameMatch
                : bankNameMatch // ignore: cast_nullable_to_non_nullable
                      as String,
            bankVerifiedAt: freezed == bankVerifiedAt
                ? _value.bankVerifiedAt
                : bankVerifiedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            reviewedAt: freezed == reviewedAt
                ? _value.reviewedAt
                : reviewedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            expiresAt: freezed == expiresAt
                ? _value.expiresAt
                : expiresAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
            updatedAt: freezed == updatedAt
                ? _value.updatedAt
                : updatedAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$IdentityStatusDtoImplCopyWith<$Res>
    implements $IdentityStatusDtoCopyWith<$Res> {
  factory _$$IdentityStatusDtoImplCopyWith(
    _$IdentityStatusDtoImpl value,
    $Res Function(_$IdentityStatusDtoImpl) then,
  ) = __$$IdentityStatusDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String? level,
    String status,
    @JsonKey(name: 'govt_id_kind') String? govtIdKind,
    @JsonKey(name: 'govt_id_last4') String? govtIdLast4,
    @JsonKey(name: 'pan_last4') String? panLast4,
    @JsonKey(name: 'bank_last4') String? bankLast4,
    @JsonKey(name: 'govt_id_status') String govtIdStatus,
    @JsonKey(name: 'pan_status') String panStatus,
    @JsonKey(name: 'bank_status') String bankStatus,
    @JsonKey(name: 'penny_drop_status') String pennyDropStatus,
    @JsonKey(name: 'bank_name_match') String bankNameMatch,
    @JsonKey(name: 'bank_verified_at') DateTime? bankVerifiedAt,
    @JsonKey(name: 'reviewed_at') DateTime? reviewedAt,
    @JsonKey(name: 'expires_at') DateTime? expiresAt,
    @JsonKey(name: 'updated_at') DateTime? updatedAt,
  });
}

/// @nodoc
class __$$IdentityStatusDtoImplCopyWithImpl<$Res>
    extends _$IdentityStatusDtoCopyWithImpl<$Res, _$IdentityStatusDtoImpl>
    implements _$$IdentityStatusDtoImplCopyWith<$Res> {
  __$$IdentityStatusDtoImplCopyWithImpl(
    _$IdentityStatusDtoImpl _value,
    $Res Function(_$IdentityStatusDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of IdentityStatusDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? level = freezed,
    Object? status = null,
    Object? govtIdKind = freezed,
    Object? govtIdLast4 = freezed,
    Object? panLast4 = freezed,
    Object? bankLast4 = freezed,
    Object? govtIdStatus = null,
    Object? panStatus = null,
    Object? bankStatus = null,
    Object? pennyDropStatus = null,
    Object? bankNameMatch = null,
    Object? bankVerifiedAt = freezed,
    Object? reviewedAt = freezed,
    Object? expiresAt = freezed,
    Object? updatedAt = freezed,
  }) {
    return _then(
      _$IdentityStatusDtoImpl(
        level: freezed == level
            ? _value.level
            : level // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        govtIdKind: freezed == govtIdKind
            ? _value.govtIdKind
            : govtIdKind // ignore: cast_nullable_to_non_nullable
                  as String?,
        govtIdLast4: freezed == govtIdLast4
            ? _value.govtIdLast4
            : govtIdLast4 // ignore: cast_nullable_to_non_nullable
                  as String?,
        panLast4: freezed == panLast4
            ? _value.panLast4
            : panLast4 // ignore: cast_nullable_to_non_nullable
                  as String?,
        bankLast4: freezed == bankLast4
            ? _value.bankLast4
            : bankLast4 // ignore: cast_nullable_to_non_nullable
                  as String?,
        govtIdStatus: null == govtIdStatus
            ? _value.govtIdStatus
            : govtIdStatus // ignore: cast_nullable_to_non_nullable
                  as String,
        panStatus: null == panStatus
            ? _value.panStatus
            : panStatus // ignore: cast_nullable_to_non_nullable
                  as String,
        bankStatus: null == bankStatus
            ? _value.bankStatus
            : bankStatus // ignore: cast_nullable_to_non_nullable
                  as String,
        pennyDropStatus: null == pennyDropStatus
            ? _value.pennyDropStatus
            : pennyDropStatus // ignore: cast_nullable_to_non_nullable
                  as String,
        bankNameMatch: null == bankNameMatch
            ? _value.bankNameMatch
            : bankNameMatch // ignore: cast_nullable_to_non_nullable
                  as String,
        bankVerifiedAt: freezed == bankVerifiedAt
            ? _value.bankVerifiedAt
            : bankVerifiedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        reviewedAt: freezed == reviewedAt
            ? _value.reviewedAt
            : reviewedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        expiresAt: freezed == expiresAt
            ? _value.expiresAt
            : expiresAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
        updatedAt: freezed == updatedAt
            ? _value.updatedAt
            : updatedAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$IdentityStatusDtoImpl implements _IdentityStatusDto {
  const _$IdentityStatusDtoImpl({
    this.level,
    this.status = 'unverified',
    @JsonKey(name: 'govt_id_kind') this.govtIdKind,
    @JsonKey(name: 'govt_id_last4') this.govtIdLast4,
    @JsonKey(name: 'pan_last4') this.panLast4,
    @JsonKey(name: 'bank_last4') this.bankLast4,
    @JsonKey(name: 'govt_id_status') this.govtIdStatus = 'NotStarted',
    @JsonKey(name: 'pan_status') this.panStatus = 'NotStarted',
    @JsonKey(name: 'bank_status') this.bankStatus = 'NotStarted',
    @JsonKey(name: 'penny_drop_status') this.pennyDropStatus = 'NotStarted',
    @JsonKey(name: 'bank_name_match') this.bankNameMatch = 'NotChecked',
    @JsonKey(name: 'bank_verified_at') this.bankVerifiedAt,
    @JsonKey(name: 'reviewed_at') this.reviewedAt,
    @JsonKey(name: 'expires_at') this.expiresAt,
    @JsonKey(name: 'updated_at') this.updatedAt,
  });

  factory _$IdentityStatusDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$IdentityStatusDtoImplFromJson(json);

  /// L0–L5 trust label.
  @override
  final String? level;

  /// `unverified` / `pending` / `verified` / `rejected`.
  @override
  @JsonKey()
  final String status;
  @override
  @JsonKey(name: 'govt_id_kind')
  final String? govtIdKind;
  @override
  @JsonKey(name: 'govt_id_last4')
  final String? govtIdLast4;
  @override
  @JsonKey(name: 'pan_last4')
  final String? panLast4;
  @override
  @JsonKey(name: 'bank_last4')
  final String? bankLast4;
  // Per-component state. The aggregate `status` above cannot answer "is my PAN verified" — every
  // submission wrote it, so one failed bank check reported the whole identity rejected. Defaulted
  // so a client pinned to an older backend still parses.
  @override
  @JsonKey(name: 'govt_id_status')
  final String govtIdStatus;
  @override
  @JsonKey(name: 'pan_status')
  final String panStatus;
  @override
  @JsonKey(name: 'bank_status')
  final String bankStatus;

  /// Penny drop proves the account exists and accepts deposits; `bankLast4` alone only ever meant
  /// a number was typed.
  @override
  @JsonKey(name: 'penny_drop_status')
  final String pennyDropStatus;

  /// Whether the bank's registered holder name matched — the control that catches a PAN and a bank
  /// account belonging to two different people.
  @override
  @JsonKey(name: 'bank_name_match')
  final String bankNameMatch;
  @override
  @JsonKey(name: 'bank_verified_at')
  final DateTime? bankVerifiedAt;
  @override
  @JsonKey(name: 'reviewed_at')
  final DateTime? reviewedAt;
  @override
  @JsonKey(name: 'expires_at')
  final DateTime? expiresAt;
  @override
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  @override
  String toString() {
    return 'IdentityStatusDto(level: $level, status: $status, govtIdKind: $govtIdKind, govtIdLast4: $govtIdLast4, panLast4: $panLast4, bankLast4: $bankLast4, govtIdStatus: $govtIdStatus, panStatus: $panStatus, bankStatus: $bankStatus, pennyDropStatus: $pennyDropStatus, bankNameMatch: $bankNameMatch, bankVerifiedAt: $bankVerifiedAt, reviewedAt: $reviewedAt, expiresAt: $expiresAt, updatedAt: $updatedAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$IdentityStatusDtoImpl &&
            (identical(other.level, level) || other.level == level) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.govtIdKind, govtIdKind) ||
                other.govtIdKind == govtIdKind) &&
            (identical(other.govtIdLast4, govtIdLast4) ||
                other.govtIdLast4 == govtIdLast4) &&
            (identical(other.panLast4, panLast4) ||
                other.panLast4 == panLast4) &&
            (identical(other.bankLast4, bankLast4) ||
                other.bankLast4 == bankLast4) &&
            (identical(other.govtIdStatus, govtIdStatus) ||
                other.govtIdStatus == govtIdStatus) &&
            (identical(other.panStatus, panStatus) ||
                other.panStatus == panStatus) &&
            (identical(other.bankStatus, bankStatus) ||
                other.bankStatus == bankStatus) &&
            (identical(other.pennyDropStatus, pennyDropStatus) ||
                other.pennyDropStatus == pennyDropStatus) &&
            (identical(other.bankNameMatch, bankNameMatch) ||
                other.bankNameMatch == bankNameMatch) &&
            (identical(other.bankVerifiedAt, bankVerifiedAt) ||
                other.bankVerifiedAt == bankVerifiedAt) &&
            (identical(other.reviewedAt, reviewedAt) ||
                other.reviewedAt == reviewedAt) &&
            (identical(other.expiresAt, expiresAt) ||
                other.expiresAt == expiresAt) &&
            (identical(other.updatedAt, updatedAt) ||
                other.updatedAt == updatedAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    level,
    status,
    govtIdKind,
    govtIdLast4,
    panLast4,
    bankLast4,
    govtIdStatus,
    panStatus,
    bankStatus,
    pennyDropStatus,
    bankNameMatch,
    bankVerifiedAt,
    reviewedAt,
    expiresAt,
    updatedAt,
  );

  /// Create a copy of IdentityStatusDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$IdentityStatusDtoImplCopyWith<_$IdentityStatusDtoImpl> get copyWith =>
      __$$IdentityStatusDtoImplCopyWithImpl<_$IdentityStatusDtoImpl>(
        this,
        _$identity,
      );

  @override
  Map<String, dynamic> toJson() {
    return _$$IdentityStatusDtoImplToJson(this);
  }
}

abstract class _IdentityStatusDto implements IdentityStatusDto {
  const factory _IdentityStatusDto({
    final String? level,
    final String status,
    @JsonKey(name: 'govt_id_kind') final String? govtIdKind,
    @JsonKey(name: 'govt_id_last4') final String? govtIdLast4,
    @JsonKey(name: 'pan_last4') final String? panLast4,
    @JsonKey(name: 'bank_last4') final String? bankLast4,
    @JsonKey(name: 'govt_id_status') final String govtIdStatus,
    @JsonKey(name: 'pan_status') final String panStatus,
    @JsonKey(name: 'bank_status') final String bankStatus,
    @JsonKey(name: 'penny_drop_status') final String pennyDropStatus,
    @JsonKey(name: 'bank_name_match') final String bankNameMatch,
    @JsonKey(name: 'bank_verified_at') final DateTime? bankVerifiedAt,
    @JsonKey(name: 'reviewed_at') final DateTime? reviewedAt,
    @JsonKey(name: 'expires_at') final DateTime? expiresAt,
    @JsonKey(name: 'updated_at') final DateTime? updatedAt,
  }) = _$IdentityStatusDtoImpl;

  factory _IdentityStatusDto.fromJson(Map<String, dynamic> json) =
      _$IdentityStatusDtoImpl.fromJson;

  /// L0–L5 trust label.
  @override
  String? get level;

  /// `unverified` / `pending` / `verified` / `rejected`.
  @override
  String get status;
  @override
  @JsonKey(name: 'govt_id_kind')
  String? get govtIdKind;
  @override
  @JsonKey(name: 'govt_id_last4')
  String? get govtIdLast4;
  @override
  @JsonKey(name: 'pan_last4')
  String? get panLast4;
  @override
  @JsonKey(name: 'bank_last4')
  String? get bankLast4; // Per-component state. The aggregate `status` above cannot answer "is my PAN verified" — every
  // submission wrote it, so one failed bank check reported the whole identity rejected. Defaulted
  // so a client pinned to an older backend still parses.
  @override
  @JsonKey(name: 'govt_id_status')
  String get govtIdStatus;
  @override
  @JsonKey(name: 'pan_status')
  String get panStatus;
  @override
  @JsonKey(name: 'bank_status')
  String get bankStatus;

  /// Penny drop proves the account exists and accepts deposits; `bankLast4` alone only ever meant
  /// a number was typed.
  @override
  @JsonKey(name: 'penny_drop_status')
  String get pennyDropStatus;

  /// Whether the bank's registered holder name matched — the control that catches a PAN and a bank
  /// account belonging to two different people.
  @override
  @JsonKey(name: 'bank_name_match')
  String get bankNameMatch;
  @override
  @JsonKey(name: 'bank_verified_at')
  DateTime? get bankVerifiedAt;
  @override
  @JsonKey(name: 'reviewed_at')
  DateTime? get reviewedAt;
  @override
  @JsonKey(name: 'expires_at')
  DateTime? get expiresAt;
  @override
  @JsonKey(name: 'updated_at')
  DateTime? get updatedAt;

  /// Create a copy of IdentityStatusDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$IdentityStatusDtoImplCopyWith<_$IdentityStatusDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

RefundDto _$RefundDtoFromJson(Map<String, dynamic> json) {
  return _RefundDto.fromJson(json);
}

/// @nodoc
mixin _$RefundDto {
  String get id => throw _privateConstructorUsedError;
  @JsonKey(name: 'order_id')
  String get orderId => throw _privateConstructorUsedError;
  @JsonKey(name: 'event_id')
  String? get eventId => throw _privateConstructorUsedError;
  @JsonKey(name: 'org_id')
  String? get orgId => throw _privateConstructorUsedError;
  @JsonKey(name: 'amount_paise')
  int get amountPaise => throw _privateConstructorUsedError;
  String get currency => throw _privateConstructorUsedError;
  String? get reason => throw _privateConstructorUsedError;

  /// Lowercased server-side: `pending` / `processing` / `succeeded` / `failed`.
  String get status => throw _privateConstructorUsedError;
  @JsonKey(name: 'razorpay_refund_id')
  String? get razorpayRefundId => throw _privateConstructorUsedError;
  @JsonKey(name: 'created_at')
  DateTime? get createdAt => throw _privateConstructorUsedError;

  /// Serializes this RefundDto to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of RefundDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $RefundDtoCopyWith<RefundDto> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $RefundDtoCopyWith<$Res> {
  factory $RefundDtoCopyWith(RefundDto value, $Res Function(RefundDto) then) =
      _$RefundDtoCopyWithImpl<$Res, RefundDto>;
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'order_id') String orderId,
    @JsonKey(name: 'event_id') String? eventId,
    @JsonKey(name: 'org_id') String? orgId,
    @JsonKey(name: 'amount_paise') int amountPaise,
    String currency,
    String? reason,
    String status,
    @JsonKey(name: 'razorpay_refund_id') String? razorpayRefundId,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class _$RefundDtoCopyWithImpl<$Res, $Val extends RefundDto>
    implements $RefundDtoCopyWith<$Res> {
  _$RefundDtoCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of RefundDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orderId = null,
    Object? eventId = freezed,
    Object? orgId = freezed,
    Object? amountPaise = null,
    Object? currency = null,
    Object? reason = freezed,
    Object? status = null,
    Object? razorpayRefundId = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _value.copyWith(
            id: null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                      as String,
            orderId: null == orderId
                ? _value.orderId
                : orderId // ignore: cast_nullable_to_non_nullable
                      as String,
            eventId: freezed == eventId
                ? _value.eventId
                : eventId // ignore: cast_nullable_to_non_nullable
                      as String?,
            orgId: freezed == orgId
                ? _value.orgId
                : orgId // ignore: cast_nullable_to_non_nullable
                      as String?,
            amountPaise: null == amountPaise
                ? _value.amountPaise
                : amountPaise // ignore: cast_nullable_to_non_nullable
                      as int,
            currency: null == currency
                ? _value.currency
                : currency // ignore: cast_nullable_to_non_nullable
                      as String,
            reason: freezed == reason
                ? _value.reason
                : reason // ignore: cast_nullable_to_non_nullable
                      as String?,
            status: null == status
                ? _value.status
                : status // ignore: cast_nullable_to_non_nullable
                      as String,
            razorpayRefundId: freezed == razorpayRefundId
                ? _value.razorpayRefundId
                : razorpayRefundId // ignore: cast_nullable_to_non_nullable
                      as String?,
            createdAt: freezed == createdAt
                ? _value.createdAt
                : createdAt // ignore: cast_nullable_to_non_nullable
                      as DateTime?,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$RefundDtoImplCopyWith<$Res>
    implements $RefundDtoCopyWith<$Res> {
  factory _$$RefundDtoImplCopyWith(
    _$RefundDtoImpl value,
    $Res Function(_$RefundDtoImpl) then,
  ) = __$$RefundDtoImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    @JsonKey(name: 'order_id') String orderId,
    @JsonKey(name: 'event_id') String? eventId,
    @JsonKey(name: 'org_id') String? orgId,
    @JsonKey(name: 'amount_paise') int amountPaise,
    String currency,
    String? reason,
    String status,
    @JsonKey(name: 'razorpay_refund_id') String? razorpayRefundId,
    @JsonKey(name: 'created_at') DateTime? createdAt,
  });
}

/// @nodoc
class __$$RefundDtoImplCopyWithImpl<$Res>
    extends _$RefundDtoCopyWithImpl<$Res, _$RefundDtoImpl>
    implements _$$RefundDtoImplCopyWith<$Res> {
  __$$RefundDtoImplCopyWithImpl(
    _$RefundDtoImpl _value,
    $Res Function(_$RefundDtoImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of RefundDto
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? orderId = null,
    Object? eventId = freezed,
    Object? orgId = freezed,
    Object? amountPaise = null,
    Object? currency = null,
    Object? reason = freezed,
    Object? status = null,
    Object? razorpayRefundId = freezed,
    Object? createdAt = freezed,
  }) {
    return _then(
      _$RefundDtoImpl(
        id: null == id
            ? _value.id
            : id // ignore: cast_nullable_to_non_nullable
                  as String,
        orderId: null == orderId
            ? _value.orderId
            : orderId // ignore: cast_nullable_to_non_nullable
                  as String,
        eventId: freezed == eventId
            ? _value.eventId
            : eventId // ignore: cast_nullable_to_non_nullable
                  as String?,
        orgId: freezed == orgId
            ? _value.orgId
            : orgId // ignore: cast_nullable_to_non_nullable
                  as String?,
        amountPaise: null == amountPaise
            ? _value.amountPaise
            : amountPaise // ignore: cast_nullable_to_non_nullable
                  as int,
        currency: null == currency
            ? _value.currency
            : currency // ignore: cast_nullable_to_non_nullable
                  as String,
        reason: freezed == reason
            ? _value.reason
            : reason // ignore: cast_nullable_to_non_nullable
                  as String?,
        status: null == status
            ? _value.status
            : status // ignore: cast_nullable_to_non_nullable
                  as String,
        razorpayRefundId: freezed == razorpayRefundId
            ? _value.razorpayRefundId
            : razorpayRefundId // ignore: cast_nullable_to_non_nullable
                  as String?,
        createdAt: freezed == createdAt
            ? _value.createdAt
            : createdAt // ignore: cast_nullable_to_non_nullable
                  as DateTime?,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$RefundDtoImpl implements _RefundDto {
  const _$RefundDtoImpl({
    required this.id,
    @JsonKey(name: 'order_id') required this.orderId,
    @JsonKey(name: 'event_id') this.eventId,
    @JsonKey(name: 'org_id') this.orgId,
    @JsonKey(name: 'amount_paise') this.amountPaise = 0,
    this.currency = 'INR',
    this.reason,
    this.status = 'pending',
    @JsonKey(name: 'razorpay_refund_id') this.razorpayRefundId,
    @JsonKey(name: 'created_at') this.createdAt,
  });

  factory _$RefundDtoImpl.fromJson(Map<String, dynamic> json) =>
      _$$RefundDtoImplFromJson(json);

  @override
  final String id;
  @override
  @JsonKey(name: 'order_id')
  final String orderId;
  @override
  @JsonKey(name: 'event_id')
  final String? eventId;
  @override
  @JsonKey(name: 'org_id')
  final String? orgId;
  @override
  @JsonKey(name: 'amount_paise')
  final int amountPaise;
  @override
  @JsonKey()
  final String currency;
  @override
  final String? reason;

  /// Lowercased server-side: `pending` / `processing` / `succeeded` / `failed`.
  @override
  @JsonKey()
  final String status;
  @override
  @JsonKey(name: 'razorpay_refund_id')
  final String? razorpayRefundId;
  @override
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  @override
  String toString() {
    return 'RefundDto(id: $id, orderId: $orderId, eventId: $eventId, orgId: $orgId, amountPaise: $amountPaise, currency: $currency, reason: $reason, status: $status, razorpayRefundId: $razorpayRefundId, createdAt: $createdAt)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$RefundDtoImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.orderId, orderId) || other.orderId == orderId) &&
            (identical(other.eventId, eventId) || other.eventId == eventId) &&
            (identical(other.orgId, orgId) || other.orgId == orgId) &&
            (identical(other.amountPaise, amountPaise) ||
                other.amountPaise == amountPaise) &&
            (identical(other.currency, currency) ||
                other.currency == currency) &&
            (identical(other.reason, reason) || other.reason == reason) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.razorpayRefundId, razorpayRefundId) ||
                other.razorpayRefundId == razorpayRefundId) &&
            (identical(other.createdAt, createdAt) ||
                other.createdAt == createdAt));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    orderId,
    eventId,
    orgId,
    amountPaise,
    currency,
    reason,
    status,
    razorpayRefundId,
    createdAt,
  );

  /// Create a copy of RefundDto
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$RefundDtoImplCopyWith<_$RefundDtoImpl> get copyWith =>
      __$$RefundDtoImplCopyWithImpl<_$RefundDtoImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$RefundDtoImplToJson(this);
  }
}

abstract class _RefundDto implements RefundDto {
  const factory _RefundDto({
    required final String id,
    @JsonKey(name: 'order_id') required final String orderId,
    @JsonKey(name: 'event_id') final String? eventId,
    @JsonKey(name: 'org_id') final String? orgId,
    @JsonKey(name: 'amount_paise') final int amountPaise,
    final String currency,
    final String? reason,
    final String status,
    @JsonKey(name: 'razorpay_refund_id') final String? razorpayRefundId,
    @JsonKey(name: 'created_at') final DateTime? createdAt,
  }) = _$RefundDtoImpl;

  factory _RefundDto.fromJson(Map<String, dynamic> json) =
      _$RefundDtoImpl.fromJson;

  @override
  String get id;
  @override
  @JsonKey(name: 'order_id')
  String get orderId;
  @override
  @JsonKey(name: 'event_id')
  String? get eventId;
  @override
  @JsonKey(name: 'org_id')
  String? get orgId;
  @override
  @JsonKey(name: 'amount_paise')
  int get amountPaise;
  @override
  String get currency;
  @override
  String? get reason;

  /// Lowercased server-side: `pending` / `processing` / `succeeded` / `failed`.
  @override
  String get status;
  @override
  @JsonKey(name: 'razorpay_refund_id')
  String? get razorpayRefundId;
  @override
  @JsonKey(name: 'created_at')
  DateTime? get createdAt;

  /// Create a copy of RefundDto
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$RefundDtoImplCopyWith<_$RefundDtoImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
