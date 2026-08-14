import 'package:freezed_annotation/freezed_annotation.dart';

import '../../domain/entities/event_category.dart';

part 'event_category_dto.freezed.dart';
part 'event_category_dto.g.dart';

/// Mirrors the snake_case category JSON (`/v1/categories`).
@freezed
class EventCategoryDto with _$EventCategoryDto {
  const EventCategoryDto._();

  const factory EventCategoryDto({
    required String id,
    required String name,
    @Default('category') String level,
    @JsonKey(name: 'parent_id') String? parentId,
    @JsonKey(name: 'is_visible') @Default(true) bool isVisible,
    /// D-305 — "Public"/"Private" on a Type node, used by the Create-Event gate to offer only the
    /// Types the chosen product class permits. Null = Public, matching the server's own fallback.
    @JsonKey(name: 'product_class') String? productClass,
  }) = _EventCategoryDto;

  factory EventCategoryDto.fromJson(Map<String, dynamic> json) =>
      _$EventCategoryDtoFromJson(json);

  EventCategory toEntity() => EventCategory(
        id: id,
        name: name,
        level: level,
        parentId: parentId,
        productClass: productClass,
      );
}
