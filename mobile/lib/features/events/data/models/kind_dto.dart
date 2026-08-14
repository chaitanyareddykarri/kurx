import 'package:freezed_annotation/freezed_annotation.dart';

import '../../domain/entities/event_kind.dart';

part 'kind_dto.freezed.dart';
part 'kind_dto.g.dart';

/// Mirrors the `/v1/kinds` JSON (V3 §15, Phase 16 — D-184).
@freezed
class KindDto with _$KindDto {
  const KindDto._();

  const factory KindDto({
    required String slug,
    required String name,
    @JsonKey(name: 'group_slug') required String groupSlug,
    @JsonKey(name: 'group_name') required String groupName,
    required int sort,
  }) = _KindDto;

  factory KindDto.fromJson(Map<String, dynamic> json) => _$KindDtoFromJson(json);

  EventKind toEntity() =>
      EventKind(slug: slug, name: name, groupSlug: groupSlug, groupName: groupName, sort: sort);
}
