/// DTOs for the trusted-device authentication platform (AM2–AM9).
///
/// Hand-written rather than freezed-generated: these are read-only response shapes with no
/// copyWith/equality needs, and keeping them codegen-free means `flutter test` needs no
/// build_runner pass to stay green.
///
/// Request bodies are camelCase, responses snake_case (D-019) — except the endpoints that return
/// records projected straight from C# records (`/devices`, `/sessions`, `/login/pending`), which
/// are camelCase because ASP.NET's default JSON policy applies to those. Each DTO documents which
/// convention its own endpoint uses so the mismatch never has to be rediscovered.
library;

/// `POST /v1/auth/devices/enroll` — snake_case.
class DeviceEnrollmentDto {
  const DeviceEnrollmentDto({
    required this.deviceId,
    required this.challengeId,
    required this.nonce,
    required this.matchNumber,
    required this.expiresAt,
  });

  final String deviceId;
  final String challengeId;
  final String nonce;
  final int matchNumber;
  final DateTime expiresAt;

  factory DeviceEnrollmentDto.fromJson(Map<String, dynamic> json) => DeviceEnrollmentDto(
        deviceId: json['device_id'] as String,
        challengeId: json['challenge_id'] as String,
        nonce: json['nonce'] as String,
        matchNumber: (json['match_number'] as num).toInt(),
        expiresAt: DateTime.parse(json['expires_at'] as String),
      );
}

/// `GET /v1/auth/devices` and `GET /v1/auth/passkeys` — camelCase (C# record projection).
class TrustedDeviceDto {
  const TrustedDeviceDto({
    required this.id,
    required this.name,
    required this.platform,
    required this.state,
    required this.lastSeenAt,
    required this.createdAt,
  });

  final String id;
  final String? name;
  final String platform;

  /// Lifecycle FSM value (ADR-AM12): PendingVerification | Trusted | Suspended | Revoked | …
  final String state;
  final DateTime? lastSeenAt;
  final DateTime createdAt;

  bool get isTrusted => state == 'Trusted';

  factory TrustedDeviceDto.fromJson(Map<String, dynamic> json) => TrustedDeviceDto(
        id: json['id'] as String,
        name: json['name'] as String?,
        platform: json['platform'] as String? ?? 'unknown',
        state: json['state'] as String? ?? 'unknown',
        lastSeenAt: (json['last_seen_at'] ?? json['lastSeenAt']) == null ? null : DateTime.parse((json['last_seen_at'] ?? json['lastSeenAt']) as String),
        createdAt: DateTime.parse((json['created_at'] ?? json['createdAt']) as String),
      );
}

/// `GET /v1/auth/login/pending` — camelCase. The nonce here is what the device signs.
class PendingLoginDto {
  const PendingLoginDto({
    required this.challengeId,
    required this.nonce,
    required this.matchNumber,
    required this.contextJson,
    required this.expiresAt,
  });

  final String challengeId;
  final String nonce;

  /// Two-digit number the approving user must match against the number on the waiting screen —
  /// the anti-push-fatigue defence (ADR-A9). Must be shown, never auto-confirmed.
  final int? matchNumber;

  /// Raw JSON context (ip / user agent) so the approval sheet can show *where* the request came from.
  final String? contextJson;
  final DateTime expiresAt;

  factory PendingLoginDto.fromJson(Map<String, dynamic> json) => PendingLoginDto(
        challengeId: (json['challenge_id'] ?? json['challengeId']) as String,
        nonce: json['nonce'] as String,
        matchNumber: ((json['match_number'] ?? json['matchNumber']) as num?)?.toInt(),
        contextJson: (json['context_json'] ?? json['contextJson']) as String?,
        expiresAt: DateTime.parse((json['expires_at'] ?? json['expiresAt']) as String),
      );
}

/// `POST /v1/auth/passkeys/{register,login}/options` — snake_case wrapper around an opaque
/// W3C WebAuthn options object.
///
/// [optionsJson] is **deliberately kept as a raw map and never modelled**: it is handed verbatim to
/// Credential Manager, which understands the W3C serialization natively. Typing it here would mean
/// re-implementing the WebAuthn schema in Dart and creating a second place for it to drift from the
/// server's (D-097).
class PasskeyCeremonyDto {
  const PasskeyCeremonyDto({required this.challengeId, required this.optionsJson});

  final String challengeId;
  final Map<String, dynamic> optionsJson;

  factory PasskeyCeremonyDto.fromJson(Map<String, dynamic> json) => PasskeyCeremonyDto(
        challengeId: json['challenge_id'] as String,
        optionsJson: (json['options'] as Map).cast<String, dynamic>(),
      );
}

/// `POST /v1/auth/login/password` — snake_case. Either an immediate session (factor 2 was a valid
/// trusted-browser cookie) or a `device_approval` challenge to wait on. On mobile "remember browser"
/// never applies — the trusted *device key* is factor 2, not a browser cookie — so this client always
/// sends `rememberBrowser:false`; the session branch is the already-trusted case, device_approval the
/// common one (D-182).
class PasswordLoginDto {
  const PasswordLoginDto.session({
    required this.accessToken,
    required this.refreshToken,
  })  : challengeId = null,
        pollToken = null,
        matchNumber = null,
        expiresAt = null,
        methods = const [];

  const PasswordLoginDto.deviceApproval({
    required this.challengeId,
    required this.pollToken,
    required this.matchNumber,
    required this.expiresAt,
    this.methods = const [],
  })  : accessToken = null,
        refreshToken = null;

  const PasswordLoginDto.secondFactor({
    required this.challengeId,
    required this.pollToken,
    required this.expiresAt,
    required this.methods,
  })  : accessToken = null,
        refreshToken = null,
        matchNumber = null;

  final String? accessToken;
  final String? refreshToken;
  final String? challengeId;
  final String? pollToken;
  final int? matchNumber;
  final DateTime? expiresAt;

  /// What the backend says this account can use, already ranked (D-283).
  final List<SecondFactorMethodDto> methods;

  bool get needsDeviceApproval => challengeId != null && matchNumber != null;

  /// The backend offered a choice of second factors (D-280). Distinguished from device approval by the
  /// absence of a match number: that ceremony is the only one with digits to confirm.
  bool get needsSecondFactor => challengeId != null && matchNumber == null;

  factory PasswordLoginDto.fromJson(Map<String, dynamic> json) {
    final methods = SecondFactorMethodDto.listFrom(json['methods']);
    if (json['next'] == 'device_approval') {
      return PasswordLoginDto.deviceApproval(
        challengeId: json['challenge_id'] as String,
        pollToken: json['poll_token'] as String,
        matchNumber: (json['match_number'] as num).toInt(),
        expiresAt: DateTime.parse(json['expires_at'] as String),
        methods: methods,
      );
    }
    if (json['next'] == 'second_factor') {
      return PasswordLoginDto.secondFactor(
        challengeId: json['challenge_id'] as String,
        pollToken: json['poll_token'] as String,
        expiresAt: DateTime.parse(json['expires_at'] as String),
        methods: methods,
      );
    }
    return PasswordLoginDto.session(
      accessToken: json['access_token'] as String,
      refreshToken: json['refresh_token'] as String,
    );
  }
}

/// One second factor the backend says this account can use (D-283).
///
/// The client renders these and never decides them — there is deliberately no local list of methods and no
/// local ranking anywhere in this app. [method] is the stable key to switch on; [label] is already
/// localised server-side, so never parse it.
class SecondFactorMethodDto {
  const SecondFactorMethodDto({
    required this.method,
    required this.rank,
    required this.label,
    this.hint,
  });

  final String method;
  final int rank;
  final String label;
  final String? hint;

  /// True when this method is satisfied by a code the server delivers, rather than by its own ceremony.
  bool get isCodeBased => method == 'sms_otp' || method == 'email_otp';

  factory SecondFactorMethodDto.fromJson(Map<String, dynamic> json) => SecondFactorMethodDto(
        method: json['method'] as String,
        rank: (json['rank'] as num).toInt(),
        label: json['label'] as String,
        hint: json['hint'] as String?,
      );

  /// Tolerant by design: a payload without `methods`, or with an unreadable one, yields an empty list
  /// rather than throwing. A login screen that crashes on an unexpected field is a lockout.
  static List<SecondFactorMethodDto> listFrom(Object? raw) {
    if (raw is! List) return const [];
    return raw
        .whereType<Map>()
        .map((e) => SecondFactorMethodDto.fromJson(e.cast<String, dynamic>()))
        .toList();
  }
}

/// `POST /v1/auth/login/status` — snake_case. Tokens are present only on `approved`, and only once
/// (the challenge flips to Consumed in the same transaction, D-080).
class LoginStatusDto {
  const LoginStatusDto({
    required this.status,
    this.accessToken,
    this.refreshToken,
    this.userId,
  });

  /// pending | approved | rejected | expired | consumed
  final String status;
  final String? accessToken;
  final String? refreshToken;
  final String? userId;

  bool get isApproved => status == 'approved' && accessToken != null && refreshToken != null;

  /// Terminal outcomes the waiting screen must stop polling on.
  bool get isTerminal => status == 'rejected' || status == 'expired' || status == 'consumed';

  factory LoginStatusDto.fromJson(Map<String, dynamic> json) => LoginStatusDto(
        status: json['status'] as String? ?? 'pending',
        accessToken: json['access_token'] as String?,
        refreshToken: json['refresh_token'] as String?,
        userId: json['user_id'] as String?,
      );
}

/// `GET /v1/auth/security-center` — snake_case. Aggregate counts for the Security Center overview
/// (Phase 2E); the per-factor lists come from their own endpoints.
class SecurityOverviewDto {
  const SecurityOverviewDto({
    required this.hasPassword,
    this.email,
    required this.emailVerified,
    this.phone,
    required this.phoneVerified,
    required this.trustedBrowsers,
    required this.trustedDevices,
    required this.passkeys,
    required this.activeSessions,
    required this.recoveryCodesRemaining,
    required this.stepUpSatisfied,
    required this.canStepUp,
  });

  final bool hasPassword;
  final String? email;
  final bool emailVerified;
  final String? phone;
  final bool phoneVerified;
  final int trustedBrowsers;
  final int trustedDevices;
  final int passkeys;
  final int activeSessions;
  final int recoveryCodesRemaining;
  final bool stepUpSatisfied;
  final bool canStepUp;

  factory SecurityOverviewDto.fromJson(Map<String, dynamic> json) => SecurityOverviewDto(
        hasPassword: json['has_password'] as bool? ?? false,
        email: json['email'] as String?,
        emailVerified: json['email_verified'] as bool? ?? false,
        phone: json['phone'] as String?,
        phoneVerified: json['phone_verified'] as bool? ?? false,
        trustedBrowsers: (json['trusted_browsers'] as num?)?.toInt() ?? 0,
        trustedDevices: (json['trusted_devices'] as num?)?.toInt() ?? 0,
        passkeys: (json['passkeys'] as num?)?.toInt() ?? 0,
        activeSessions: (json['active_sessions'] as num?)?.toInt() ?? 0,
        recoveryCodesRemaining: (json['recovery_codes_remaining'] as num?)?.toInt() ?? 0,
        stepUpSatisfied: json['step_up_satisfied'] as bool? ?? false,
        canStepUp: json['can_step_up'] as bool? ?? false,
      );
}

/// `GET /v1/auth/security-center/activity` — snake_case. The user's own recent `security_events`.
class SecurityActivityDto {
  const SecurityActivityDto({
    required this.type,
    required this.severity,
    this.context,
    required this.createdAt,
  });

  final String type;
  final String severity;
  final String? context;
  final DateTime createdAt;

  factory SecurityActivityDto.fromJson(Map<String, dynamic> json) => SecurityActivityDto(
        type: json['type'] as String? ?? 'unknown',
        severity: json['severity'] as String? ?? 'info',
        context: json['context'] as String?,
        createdAt: DateTime.parse(json['created_at'] as String),
      );
}

/// `GET /v1/auth/trusted-browsers` — camelCase (C# record projection). A trusted browser satisfies
/// factor 2 only, never the password (INV-A).
class TrustedBrowserDto {
  const TrustedBrowserDto({
    required this.id,
    this.label,
    this.browser,
    this.operatingSystem,
    this.ip,
    this.approxLocation,
    required this.isCurrent,
    required this.createdAt,
    this.lastUsedAt,
    required this.expiresAt,
  });

  final String id;
  final String? label;
  final String? browser;
  final String? operatingSystem;
  final String? ip;
  final String? approxLocation;
  final bool isCurrent;
  final DateTime createdAt;
  final DateTime? lastUsedAt;
  final DateTime expiresAt;

  factory TrustedBrowserDto.fromJson(Map<String, dynamic> json) => TrustedBrowserDto(
        id: json['id'] as String,
        label: json['label'] as String?,
        browser: json['browser'] as String?,
        operatingSystem: (json['operating_system'] ?? json['operatingSystem']) as String?,
        ip: json['ip'] as String?,
        approxLocation: (json['approx_location'] ?? json['approxLocation']) as String?,
        isCurrent: (json['is_current'] ?? json['isCurrent']) as bool? ?? false,
        createdAt: DateTime.parse((json['created_at'] ?? json['createdAt']) as String),
        lastUsedAt: (json['last_used_at'] ?? json['lastUsedAt']) == null ? null : DateTime.parse((json['last_used_at'] ?? json['lastUsedAt']) as String),
        expiresAt: DateTime.parse((json['expires_at'] ?? json['expiresAt']) as String),
      );
}

/// `GET /v1/auth/sessions` — camelCase.
class AuthSessionDto {
  const AuthSessionDto({
    required this.id,
    required this.deviceId,
    required this.deviceName,
    required this.platform,
    required this.isCurrent,
    required this.createdAt,
    required this.lastRotatedAt,
  });

  final String id;
  final String? deviceId;
  final String? deviceName;
  final String? platform;
  final bool isCurrent;
  final DateTime createdAt;
  final DateTime? lastRotatedAt;

  factory AuthSessionDto.fromJson(Map<String, dynamic> json) => AuthSessionDto(
        id: json['id'] as String,
        deviceId: (json['device_id'] ?? json['deviceId']) as String?,
        deviceName: (json['device_name'] ?? json['deviceName']) as String?,
        platform: json['platform'] as String?,
        isCurrent: (json['is_current'] ?? json['isCurrent']) as bool? ?? false,
        createdAt: DateTime.parse((json['created_at'] ?? json['createdAt']) as String),
        lastRotatedAt:
            (json['last_rotated_at'] ?? json['lastRotatedAt']) == null ? null : DateTime.parse((json['last_rotated_at'] ?? json['lastRotatedAt']) as String),
      );
}

/// `GET /v1/auth/step-up/status` — snake_case.
class StepUpStatusDto {
  const StepUpStatusDto({
    required this.satisfied,
    required this.validUntil,
    required this.canStepUp,
  });

  final bool satisfied;
  final DateTime? validUntil;

  /// False when the account has no trusted device — the UI must not demand a step-up it cannot
  /// offer, which would lock the user out of the very screens step-up protects (D-084).
  final bool canStepUp;

  factory StepUpStatusDto.fromJson(Map<String, dynamic> json) => StepUpStatusDto(
        satisfied: json['satisfied'] as bool? ?? false,
        validUntil:
            json['valid_until'] == null ? null : DateTime.parse(json['valid_until'] as String),
        canStepUp: json['can_step_up'] as bool? ?? false,
      );
}

/// `POST /v1/auth/step-up/start` — snake_case.
class StepUpChallengeDto {
  const StepUpChallengeDto({
    required this.challengeId,
    required this.nonce,
    required this.matchNumber,
    required this.expiresAt,
  });

  final String challengeId;
  final String nonce;
  final int matchNumber;
  final DateTime expiresAt;

  factory StepUpChallengeDto.fromJson(Map<String, dynamic> json) => StepUpChallengeDto(
        challengeId: json['challenge_id'] as String,
        nonce: json['nonce'] as String,
        matchNumber: (json['match_number'] as num).toInt(),
        expiresAt: DateTime.parse(json['expires_at'] as String),
      );
}
