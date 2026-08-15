// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'org_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$OrgDtoImpl _$$OrgDtoImplFromJson(Map<String, dynamic> json) => _$OrgDtoImpl(
  id: json['id'] as String,
  name: json['name'] as String,
  slug: json['slug'] as String?,
  logoKey: json['logo_key'] as String?,
  role: json['role'] as String?,
  bio: json['bio'] as String?,
  linksJson: json['links_json'] as String?,
  payoutAccountStatus: json['payout_account_status'] as String?,
  bankLast4: json['bank_last4'] as String?,
  tier: (json['tier'] as num?)?.toInt(),
  type: json['type'] as String?,
  primaryDomain: json['primary_domain'] as String?,
  verificationStatus: json['verification_status'] as String?,
);

Map<String, dynamic> _$$OrgDtoImplToJson(_$OrgDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'slug': instance.slug,
      'logo_key': instance.logoKey,
      'role': instance.role,
      'bio': instance.bio,
      'links_json': instance.linksJson,
      'payout_account_status': instance.payoutAccountStatus,
      'bank_last4': instance.bankLast4,
      'tier': instance.tier,
      'type': instance.type,
      'primary_domain': instance.primaryDomain,
      'verification_status': instance.verificationStatus,
    };

_$OrgMemberDtoImpl _$$OrgMemberDtoImplFromJson(Map<String, dynamic> json) =>
    _$OrgMemberDtoImpl(
      userId: json['user_id'] as String,
      name: json['name'] as String,
      username: json['username'] as String?,
      phone: json['phone'] as String?,
      role: json['role'] as String,
      joinedAt: DateTime.parse(json['joined_at'] as String),
      avatarKey: json['avatar_key'] as String?,
      isVerified: json['is_verified'] as bool? ?? false,
    );

Map<String, dynamic> _$$OrgMemberDtoImplToJson(_$OrgMemberDtoImpl instance) =>
    <String, dynamic>{
      'user_id': instance.userId,
      'name': instance.name,
      'username': instance.username,
      'phone': instance.phone,
      'role': instance.role,
      'joined_at': instance.joinedAt.toIso8601String(),
      'avatar_key': instance.avatarKey,
      'is_verified': instance.isVerified,
    };

_$LedgerEntryDtoImpl _$$LedgerEntryDtoImplFromJson(Map<String, dynamic> json) =>
    _$LedgerEntryDtoImpl(
      id: json['id'] as String,
      type: json['type'] as String,
      description: json['description'] as String,
      amountPaise: (json['amount_paise'] as num).toInt(),
      balanceAfterPaise: (json['balance_after_paise'] as num).toInt(),
      createdAt: DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$$LedgerEntryDtoImplToJson(
  _$LedgerEntryDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'type': instance.type,
  'description': instance.description,
  'amount_paise': instance.amountPaise,
  'balance_after_paise': instance.balanceAfterPaise,
  'created_at': instance.createdAt.toIso8601String(),
};

_$WalletDtoImpl _$$WalletDtoImplFromJson(Map<String, dynamic> json) =>
    _$WalletDtoImpl(
      orgId: json['org_id'] as String,
      collectedPaise: (json['collected_paise'] as num?)?.toInt() ?? 0,
      availablePaise: (json['available_paise'] as num?)?.toInt() ?? 0,
      advancedPaise: (json['advanced_paise'] as num?)?.toInt() ?? 0,
      reservedPaise: (json['reserved_paise'] as num?)?.toInt() ?? 0,
      settledPaise: (json['settled_paise'] as num?)?.toInt() ?? 0,
      lifetimeEarnedPaise:
          (json['lifetime_earned_paise'] as num?)?.toInt() ?? 0,
      lifetimeWithdrawnPaise:
          (json['lifetime_withdrawn_paise'] as num?)?.toInt() ?? 0,
      currency: json['currency'] as String? ?? 'INR',
    );

Map<String, dynamic> _$$WalletDtoImplToJson(_$WalletDtoImpl instance) =>
    <String, dynamic>{
      'org_id': instance.orgId,
      'collected_paise': instance.collectedPaise,
      'available_paise': instance.availablePaise,
      'advanced_paise': instance.advancedPaise,
      'reserved_paise': instance.reservedPaise,
      'settled_paise': instance.settledPaise,
      'lifetime_earned_paise': instance.lifetimeEarnedPaise,
      'lifetime_withdrawn_paise': instance.lifetimeWithdrawnPaise,
      'currency': instance.currency,
    };

_$RepresentationDtoImpl _$$RepresentationDtoImplFromJson(
  Map<String, dynamic> json,
) => _$RepresentationDtoImpl(
  organizationId: json['organization_id'] as String,
  name: json['name'] as String,
  slug: json['slug'] as String?,
  logoKey: json['logo_key'] as String?,
  authority: json['authority'] as String,
  isVerified: json['is_verified'] as bool? ?? false,
  canBackPaidEvent: json['can_back_paid_event'] as bool? ?? false,
);

Map<String, dynamic> _$$RepresentationDtoImplToJson(
  _$RepresentationDtoImpl instance,
) => <String, dynamic>{
  'organization_id': instance.organizationId,
  'name': instance.name,
  'slug': instance.slug,
  'logo_key': instance.logoKey,
  'authority': instance.authority,
  'is_verified': instance.isVerified,
  'can_back_paid_event': instance.canBackPaidEvent,
};
