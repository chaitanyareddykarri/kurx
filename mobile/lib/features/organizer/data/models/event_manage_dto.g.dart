// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'event_manage_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$EventManageDtoImpl _$$EventManageDtoImplFromJson(Map<String, dynamic> json) =>
    _$EventManageDtoImpl(
      id: json['id'] as String,
      title: json['title'] as String,
      slug: json['slug'] as String?,
      status: json['status'] as String,
      representingOrgId: json['representing_org_id'] as String?,
      orgId: json['org_id'] as String?,
      startsAt: json['starts_at'] == null
          ? null
          : DateTime.parse(json['starts_at'] as String),
      endsAt: json['ends_at'] == null
          ? null
          : DateTime.parse(json['ends_at'] as String),
      ticketsSold: (json['tickets_sold'] as num?)?.toInt() ?? 0,
      capacity: (json['capacity'] as num?)?.toInt() ?? 0,
      revenuePaise: (json['revenue_paise'] as num?)?.toInt() ?? 0,
      checkedIn: (json['checked_in'] as num?)?.toInt() ?? 0,
      product: json['product'] as String? ?? 'Public',
      version: (json['version'] as num?)?.toInt() ?? 1,
      bannerKey: json['banner_key'] as String?,
      viewCount: (json['view_count'] as num?)?.toInt() ?? 0,
      representation: json['representation'] == null
          ? null
          : EventRepresentationDto.fromJson(
              json['representation'] as Map<String, dynamic>,
            ),
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$$EventManageDtoImplToJson(
  _$EventManageDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'title': instance.title,
  'slug': instance.slug,
  'status': instance.status,
  'representing_org_id': instance.representingOrgId,
  'org_id': instance.orgId,
  'starts_at': instance.startsAt?.toIso8601String(),
  'ends_at': instance.endsAt?.toIso8601String(),
  'tickets_sold': instance.ticketsSold,
  'capacity': instance.capacity,
  'revenue_paise': instance.revenuePaise,
  'checked_in': instance.checkedIn,
  'product': instance.product,
  'version': instance.version,
  'banner_key': instance.bannerKey,
  'view_count': instance.viewCount,
  'representation': instance.representation,
  'created_at': instance.createdAt?.toIso8601String(),
};

_$AttendeeDtoImpl _$$AttendeeDtoImplFromJson(Map<String, dynamic> json) =>
    _$AttendeeDtoImpl(
      ticketId: json['ticketId'] as String,
      code: json['code'] as String,
      state: json['state'] as String,
      checkedInAt: json['checkedInAt'] == null
          ? null
          : DateTime.parse(json['checkedInAt'] as String),
      buyerName: json['buyerName'] as String,
      buyerPhone: json['buyerPhone'] as String,
      ticketTypeName: json['ticketTypeName'] as String?,
      groupDisplayName: json['groupDisplayName'] as String?,
      buyerUserId: json['buyerUserId'] as String?,
      buyerUsername: json['buyerUsername'] as String?,
      buyerAvatarKey: json['buyerAvatarKey'] as String?,
    );

Map<String, dynamic> _$$AttendeeDtoImplToJson(_$AttendeeDtoImpl instance) =>
    <String, dynamic>{
      'ticketId': instance.ticketId,
      'code': instance.code,
      'state': instance.state,
      'checkedInAt': instance.checkedInAt?.toIso8601String(),
      'buyerName': instance.buyerName,
      'buyerPhone': instance.buyerPhone,
      'ticketTypeName': instance.ticketTypeName,
      'groupDisplayName': instance.groupDisplayName,
      'buyerUserId': instance.buyerUserId,
      'buyerUsername': instance.buyerUsername,
      'buyerAvatarKey': instance.buyerAvatarKey,
    };

_$InvitationDtoImpl _$$InvitationDtoImplFromJson(Map<String, dynamic> json) =>
    _$InvitationDtoImpl(
      id: json['id'] as String,
      email: json['email'] as String?,
      phone: json['phone'] as String?,
      name: json['name'] as String?,
      status: json['status'] as String,
      sentAt: DateTime.parse(json['sent_at'] as String),
    );

Map<String, dynamic> _$$InvitationDtoImplToJson(_$InvitationDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'email': instance.email,
      'phone': instance.phone,
      'name': instance.name,
      'status': instance.status,
      'sent_at': instance.sentAt.toIso8601String(),
    };

_$AnnouncementDtoImpl _$$AnnouncementDtoImplFromJson(
  Map<String, dynamic> json,
) => _$AnnouncementDtoImpl(
  id: json['id'] as String,
  title: json['title'] as String,
  body: json['body'] as String?,
  status: json['status'] as String,
  openCount: (json['open_count'] as num?)?.toInt() ?? 0,
  clickCount: (json['click_count'] as num?)?.toInt() ?? 0,
  sentAt: json['sent_at'] == null
      ? null
      : DateTime.parse(json['sent_at'] as String),
  createdAt: DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$$AnnouncementDtoImplToJson(
  _$AnnouncementDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'title': instance.title,
  'body': instance.body,
  'status': instance.status,
  'open_count': instance.openCount,
  'click_count': instance.clickCount,
  'sent_at': instance.sentAt?.toIso8601String(),
  'created_at': instance.createdAt.toIso8601String(),
};

_$AnalyticsDtoImpl _$$AnalyticsDtoImplFromJson(Map<String, dynamic> json) =>
    _$AnalyticsDtoImpl(
      totalRevenuePaise: (json['total_revenue_paise'] as num?)?.toInt() ?? 0,
      ticketsSold: (json['tickets_sold'] as num?)?.toInt() ?? 0,
      attendanceRate: (json['attendance_rate'] as num?)?.toDouble() ?? 0.0,
      totalViews: (json['total_views'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$$AnalyticsDtoImplToJson(_$AnalyticsDtoImpl instance) =>
    <String, dynamic>{
      'total_revenue_paise': instance.totalRevenuePaise,
      'tickets_sold': instance.ticketsSold,
      'attendance_rate': instance.attendanceRate,
      'total_views': instance.totalViews,
    };

_$AssignmentDtoImpl _$$AssignmentDtoImplFromJson(Map<String, dynamic> json) =>
    _$AssignmentDtoImpl(
      id: json['id'] as String,
      eventId: json['event_id'] as String,
      orgId: json['org_id'] as String,
      userId: json['user_id'] as String,
      role: json['role'] as String,
      customRole: json['custom_role'] as String?,
      status: json['status'] as String,
      showOnProfile: json['show_on_profile'] as bool? ?? false,
      notes: json['notes'] as String?,
      createdAt: DateTime.parse(json['created_at'] as String),
      assigneeName: json['assignee_name'] as String,
      assigneeUsername: json['assignee_username'] as String?,
      assigneeAvatarKey: json['assignee_avatar_key'] as String?,
      eventTitle: json['event_title'] as String? ?? '',
      eventSlug: json['event_slug'] as String?,
      eventStartsAt: json['event_starts_at'] == null
          ? null
          : DateTime.parse(json['event_starts_at'] as String),
      representingOrgName: json['representing_org_name'] as String?,
    );

Map<String, dynamic> _$$AssignmentDtoImplToJson(_$AssignmentDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'event_id': instance.eventId,
      'org_id': instance.orgId,
      'user_id': instance.userId,
      'role': instance.role,
      'custom_role': instance.customRole,
      'status': instance.status,
      'show_on_profile': instance.showOnProfile,
      'notes': instance.notes,
      'created_at': instance.createdAt.toIso8601String(),
      'assignee_name': instance.assigneeName,
      'assignee_username': instance.assigneeUsername,
      'assignee_avatar_key': instance.assigneeAvatarKey,
      'event_title': instance.eventTitle,
      'event_slug': instance.eventSlug,
      'event_starts_at': instance.eventStartsAt?.toIso8601String(),
      'representing_org_name': instance.representingOrgName,
    };

_$RegistrationDtoImpl _$$RegistrationDtoImplFromJson(
  Map<String, dynamic> json,
) => _$RegistrationDtoImpl(
  id: json['id'] as String,
  eventId: json['event_id'] as String,
  ticketTypeId: json['ticket_type_id'] as String?,
  subjectType: json['subject_type'] as String? ?? 'Person',
  subjectId: json['subject_id'] as String?,
  orderId: json['order_id'] as String?,
  state: json['state'] as String? ?? 'pending',
  admissionCount: (json['admission_count'] as num?)?.toInt() ?? 0,
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$$RegistrationDtoImplToJson(
  _$RegistrationDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'event_id': instance.eventId,
  'ticket_type_id': instance.ticketTypeId,
  'subject_type': instance.subjectType,
  'subject_id': instance.subjectId,
  'order_id': instance.orderId,
  'state': instance.state,
  'admission_count': instance.admissionCount,
  'created_at': instance.createdAt?.toIso8601String(),
};

_$EventRepresentationDtoImpl _$$EventRepresentationDtoImplFromJson(
  Map<String, dynamic> json,
) => _$EventRepresentationDtoImpl(
  kind: json['kind'] as String,
  organizationId: json['organization_id'] as String?,
  organizationName: json['organization_name'] as String?,
  verified: json['verified'] as bool? ?? false,
);

Map<String, dynamic> _$$EventRepresentationDtoImplToJson(
  _$EventRepresentationDtoImpl instance,
) => <String, dynamic>{
  'kind': instance.kind,
  'organization_id': instance.organizationId,
  'organization_name': instance.organizationName,
  'verified': instance.verified,
};
