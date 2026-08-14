/// `GET /v1/auth/registration/status` — snake_case (Phase 2D).
///
/// Hand-written (no freezed/build_runner) to match the trusted-device DTOs — a read-only response
/// shape the registration wizard uses to decide which steps remain. `remaining` is an ordered subset
/// of the ceremony keys: `verify_email`, `create_password`, `complete_profile`, `enroll_device`.
class RegistrationStatusDto {
  const RegistrationStatusDto({
    required this.hasPassword,
    this.email,
    required this.emailVerified,
    this.phone,
    required this.phoneVerified,
    required this.hasTrustedDevice,
    required this.needsOnboarding,
    required this.remaining,
  });

  final bool hasPassword;
  final String? email;
  final bool emailVerified;
  final String? phone;
  final bool phoneVerified;
  final bool hasTrustedDevice;
  final bool needsOnboarding;
  final List<String> remaining;

  bool get needsEmail => remaining.contains('verify_email');
  bool get needsProfile => remaining.contains('complete_profile');

  factory RegistrationStatusDto.fromJson(Map<String, dynamic> json) => RegistrationStatusDto(
        hasPassword: json['has_password'] as bool? ?? false,
        email: json['email'] as String?,
        emailVerified: json['email_verified'] as bool? ?? false,
        phone: json['phone'] as String?,
        phoneVerified: json['phone_verified'] as bool? ?? false,
        hasTrustedDevice: json['has_trusted_device'] as bool? ?? false,
        needsOnboarding: json['needs_onboarding'] as bool? ?? false,
        remaining: ((json['remaining'] as List?) ?? const []).map((e) => e as String).toList(),
      );
}
