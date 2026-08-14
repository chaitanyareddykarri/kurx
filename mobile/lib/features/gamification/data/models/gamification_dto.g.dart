// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'gamification_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$BadgeDtoImpl _$$BadgeDtoImplFromJson(Map<String, dynamic> json) =>
    _$BadgeDtoImpl(
      id: json['id'] as String,
      name: json['name'] as String,
      type: json['type'] as String,
      description: json['description'] as String,
      iconKey: json['iconKey'] as String?,
      earnedAt: DateTime.parse(json['earnedAt'] as String),
    );

Map<String, dynamic> _$$BadgeDtoImplToJson(_$BadgeDtoImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'type': instance.type,
      'description': instance.description,
      'iconKey': instance.iconKey,
      'earnedAt': instance.earnedAt.toIso8601String(),
    };

_$PointsLogEntryDtoImpl _$$PointsLogEntryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$PointsLogEntryDtoImpl(
  id: json['id'] as String,
  source: json['source'] as String,
  points: (json['points'] as num).toInt(),
  reason: json['reason'] as String?,
  createdAt: DateTime.parse(json['createdAt'] as String),
);

Map<String, dynamic> _$$PointsLogEntryDtoImplToJson(
  _$PointsLogEntryDtoImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'source': instance.source,
  'points': instance.points,
  'reason': instance.reason,
  'createdAt': instance.createdAt.toIso8601String(),
};

_$PointsSummaryDtoImpl _$$PointsSummaryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$PointsSummaryDtoImpl(
  totalPoints: (json['totalPoints'] as num).toInt(),
  history:
      (json['history'] as List<dynamic>?)
          ?.map((e) => PointsLogEntryDto.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const [],
);

Map<String, dynamic> _$$PointsSummaryDtoImplToJson(
  _$PointsSummaryDtoImpl instance,
) => <String, dynamic>{
  'totalPoints': instance.totalPoints,
  'history': instance.history,
};

_$LeaderboardEntryDtoImpl _$$LeaderboardEntryDtoImplFromJson(
  Map<String, dynamic> json,
) => _$LeaderboardEntryDtoImpl(
  rank: (json['rank'] as num).toInt(),
  userId: json['userId'] as String,
  name: json['name'] as String,
  username: json['username'] as String?,
  points: (json['points'] as num).toInt(),
);

Map<String, dynamic> _$$LeaderboardEntryDtoImplToJson(
  _$LeaderboardEntryDtoImpl instance,
) => <String, dynamic>{
  'rank': instance.rank,
  'userId': instance.userId,
  'name': instance.name,
  'username': instance.username,
  'points': instance.points,
};
