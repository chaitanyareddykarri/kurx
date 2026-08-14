import 'package:freezed_annotation/freezed_annotation.dart';

part 'token_response_dto.freezed.dart';
part 'token_response_dto.g.dart';

/// Mirrors the snake_case token response from `/v1/auth/otp/verify` and `/refresh`.
@freezed
class TokenResponseDto with _$TokenResponseDto {
  const factory TokenResponseDto({
    @JsonKey(name: 'access_token') required String accessToken,
    @JsonKey(name: 'refresh_token') required String refreshToken,
    @JsonKey(name: 'access_expires_at') DateTime? accessExpiresAt,
    @JsonKey(name: 'refresh_expires_at') DateTime? refreshExpiresAt,
    @JsonKey(name: 'user_id') String? userId,
    @JsonKey(name: 'is_new_user') @Default(false) bool isNewUser,
  }) = _TokenResponseDto;

  factory TokenResponseDto.fromJson(Map<String, dynamic> json) =>
      _$TokenResponseDtoFromJson(json);
}
