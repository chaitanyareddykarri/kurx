// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'attendee_dtos.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$TicketDtoImpl _$$TicketDtoImplFromJson(Map<String, dynamic> json) =>
    _$TicketDtoImpl(
      id: json['id'] as String,
      code: json['code'] as String,
      state: json['state'] as String? ?? 'issued',
      checkedInAt: json['checked_in_at'] == null
          ? null
          : DateTime.parse(json['checked_in_at'] as String),
      answersJson: json['answers_json'] as String?,
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$$TicketDtoImplToJson(_$TicketDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'code': instance.code,
      'state': instance.state,
      'checked_in_at': instance.checkedInAt?.toIso8601String(),
      'answers_json': instance.answersJson,
      'created_at': instance.createdAt?.toIso8601String(),
    };

_$OrderDtoImpl _$$OrderDtoImplFromJson(Map<String, dynamic> json) =>
    _$OrderDtoImpl(
      id: json['id'] as String,
      eventId: json['event_id'] as String,
      ticketTypeId: json['ticket_type_id'] as String?,
      status: json['status'] as String? ?? 'pending',
      amountPaise: (json['amount_paise'] as num?)?.toInt() ?? 0,
      razorpayOrderId: json['razorpay_order_id'] as String?,
      groupId: json['group_id'] as String?,
      joinCode: json['join_code'] as String?,
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
      tickets:
          (json['tickets'] as List<dynamic>?)
              ?.map((e) => TicketDto.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const <TicketDto>[],
      guestAccessToken: json['guest_access_token'] as String?,
    );

Map<String, dynamic> _$$OrderDtoImplToJson(_$OrderDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'event_id': instance.eventId,
      'ticket_type_id': instance.ticketTypeId,
      'status': instance.status,
      'amount_paise': instance.amountPaise,
      'razorpay_order_id': instance.razorpayOrderId,
      'group_id': instance.groupId,
      'join_code': instance.joinCode,
      'created_at': instance.createdAt?.toIso8601String(),
      'tickets': instance.tickets,
      'guest_access_token': instance.guestAccessToken,
    };

_$ReviewDtoImpl _$$ReviewDtoImplFromJson(Map<String, dynamic> json) =>
    _$ReviewDtoImpl(
      id: json['id'] as String,
      eventId: json['event_id'] as String?,
      rating: (json['rating'] as num).toInt(),
      title: json['title'] as String?,
      body: json['body'] as String?,
      isAnonymous: json['is_anonymous'] as bool? ?? false,
      isVerified: json['is_verified'] as bool? ?? false,
      authorName: json['author_name'] as String?,
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$$ReviewDtoImplToJson(_$ReviewDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'event_id': instance.eventId,
      'rating': instance.rating,
      'title': instance.title,
      'body': instance.body,
      'is_anonymous': instance.isAnonymous,
      'is_verified': instance.isVerified,
      'author_name': instance.authorName,
      'created_at': instance.createdAt?.toIso8601String(),
    };

_$WaitlistEntryDtoImpl _$$WaitlistEntryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$WaitlistEntryDtoImpl(
  id: json['id'] as String,
  eventId: json['event_id'] as String,
  ticketTypeId: json['ticket_type_id'] as String,
  position: (json['position'] as num?)?.toInt() ?? 0,
  status: json['status'] as String? ?? 'waiting',
  offerExpiresAt: json['offer_expires_at'] == null
      ? null
      : DateTime.parse(json['offer_expires_at'] as String),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$$WaitlistEntryDtoImplToJson(
  _$WaitlistEntryDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'event_id': instance.eventId,
  'ticket_type_id': instance.ticketTypeId,
  'position': instance.position,
  'status': instance.status,
  'offer_expires_at': instance.offerExpiresAt?.toIso8601String(),
  'created_at': instance.createdAt?.toIso8601String(),
};

_$ParticipationDtoImpl _$$ParticipationDtoImplFromJson(
  Map<String, dynamic> json,
) => _$ParticipationDtoImpl(
  id: json['id'] as String,
  eventId: json['event_id'] as String,
  subjectType: json['subject_type'] as String?,
  subjectId: json['subject_id'] as String?,
  roleSlug: json['role_slug'] as String,
  roleClass: json['role_class'] as String?,
  customLabel: json['custom_label'] as String?,
  state: json['state'] as String? ?? 'invited',
  visibility: json['visibility'] as String?,
  countsTowardCapacity: json['counts_toward_capacity'] as bool? ?? false,
  inventorySegment: json['inventory_segment'] as String?,
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$$ParticipationDtoImplToJson(
  _$ParticipationDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'event_id': instance.eventId,
  'subject_type': instance.subjectType,
  'subject_id': instance.subjectId,
  'role_slug': instance.roleSlug,
  'role_class': instance.roleClass,
  'custom_label': instance.customLabel,
  'state': instance.state,
  'visibility': instance.visibility,
  'counts_toward_capacity': instance.countsTowardCapacity,
  'inventory_segment': instance.inventorySegment,
  'created_at': instance.createdAt?.toIso8601String(),
};

_$OrgInvitationDtoImpl _$$OrgInvitationDtoImplFromJson(
  Map<String, dynamic> json,
) => _$OrgInvitationDtoImpl(
  id: json['id'] as String,
  orgId: json['org_id'] as String,
  orgName: json['org_name'] as String?,
  invitedPhone: json['invited_phone'] as String?,
  role: json['role'] as String? ?? 'staff',
  status: json['status'] as String? ?? 'pending',
  expiresAt: json['expires_at'] == null
      ? null
      : DateTime.parse(json['expires_at'] as String),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$$OrgInvitationDtoImplToJson(
  _$OrgInvitationDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'org_id': instance.orgId,
  'org_name': instance.orgName,
  'invited_phone': instance.invitedPhone,
  'role': instance.role,
  'status': instance.status,
  'expires_at': instance.expiresAt?.toIso8601String(),
  'created_at': instance.createdAt?.toIso8601String(),
};

_$EligibilityDtoImpl _$$EligibilityDtoImplFromJson(Map<String, dynamic> json) =>
    _$EligibilityDtoImpl(
      allowed: json['allowed'] as bool? ?? true,
      reason: json['reason'] as String?,
    );

Map<String, dynamic> _$$EligibilityDtoImplToJson(
  _$EligibilityDtoImpl instance,
) => <String, dynamic>{'allowed': instance.allowed, 'reason': instance.reason};

_$IdentityHistoryEntryDtoImpl _$$IdentityHistoryEntryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$IdentityHistoryEntryDtoImpl(
  component: json['component'] as String,
  decision: json['decision'] as String,
  reviewerId: json['reviewer_id'] as String?,
  reasonCode: json['reason_code'] as String?,
  notes: json['notes'] as String?,
  createdAt: DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$$IdentityHistoryEntryDtoImplToJson(
  _$IdentityHistoryEntryDtoImpl instance,
) => <String, dynamic>{
  'component': instance.component,
  'decision': instance.decision,
  'reviewer_id': instance.reviewerId,
  'reason_code': instance.reasonCode,
  'notes': instance.notes,
  'created_at': instance.createdAt.toIso8601String(),
};

_$IdentityStatusDtoImpl _$$IdentityStatusDtoImplFromJson(
  Map<String, dynamic> json,
) => _$IdentityStatusDtoImpl(
  level: json['level'] as String?,
  status: json['status'] as String? ?? 'unverified',
  govtIdKind: json['govt_id_kind'] as String?,
  govtIdLast4: json['govt_id_last4'] as String?,
  panLast4: json['pan_last4'] as String?,
  bankLast4: json['bank_last4'] as String?,
  govtIdStatus: json['govt_id_status'] as String? ?? 'NotStarted',
  panStatus: json['pan_status'] as String? ?? 'NotStarted',
  bankStatus: json['bank_status'] as String? ?? 'NotStarted',
  pennyDropStatus: json['penny_drop_status'] as String? ?? 'NotStarted',
  bankNameMatch: json['bank_name_match'] as String? ?? 'NotChecked',
  bankVerifiedAt: json['bank_verified_at'] == null
      ? null
      : DateTime.parse(json['bank_verified_at'] as String),
  reviewedAt: json['reviewed_at'] == null
      ? null
      : DateTime.parse(json['reviewed_at'] as String),
  expiresAt: json['expires_at'] == null
      ? null
      : DateTime.parse(json['expires_at'] as String),
  updatedAt: json['updated_at'] == null
      ? null
      : DateTime.parse(json['updated_at'] as String),
);

Map<String, dynamic> _$$IdentityStatusDtoImplToJson(
  _$IdentityStatusDtoImpl instance,
) => <String, dynamic>{
  'level': instance.level,
  'status': instance.status,
  'govt_id_kind': instance.govtIdKind,
  'govt_id_last4': instance.govtIdLast4,
  'pan_last4': instance.panLast4,
  'bank_last4': instance.bankLast4,
  'govt_id_status': instance.govtIdStatus,
  'pan_status': instance.panStatus,
  'bank_status': instance.bankStatus,
  'penny_drop_status': instance.pennyDropStatus,
  'bank_name_match': instance.bankNameMatch,
  'bank_verified_at': instance.bankVerifiedAt?.toIso8601String(),
  'reviewed_at': instance.reviewedAt?.toIso8601String(),
  'expires_at': instance.expiresAt?.toIso8601String(),
  'updated_at': instance.updatedAt?.toIso8601String(),
};

_$RefundDtoImpl _$$RefundDtoImplFromJson(Map<String, dynamic> json) =>
    _$RefundDtoImpl(
      id: json['id'] as String,
      orderId: json['order_id'] as String,
      eventId: json['event_id'] as String?,
      orgId: json['org_id'] as String?,
      amountPaise: (json['amount_paise'] as num?)?.toInt() ?? 0,
      currency: json['currency'] as String? ?? 'INR',
      reason: json['reason'] as String?,
      status: json['status'] as String? ?? 'pending',
      razorpayRefundId: json['razorpay_refund_id'] as String?,
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$$RefundDtoImplToJson(_$RefundDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'order_id': instance.orderId,
      'event_id': instance.eventId,
      'org_id': instance.orgId,
      'amount_paise': instance.amountPaise,
      'currency': instance.currency,
      'reason': instance.reason,
      'status': instance.status,
      'razorpay_refund_id': instance.razorpayRefundId,
      'created_at': instance.createdAt?.toIso8601String(),
    };
