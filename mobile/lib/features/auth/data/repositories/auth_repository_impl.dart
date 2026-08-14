import '../../../../core/storage/token_store.dart';
import '../../domain/entities/current_user.dart';
import '../../domain/repositories/auth_repository.dart';
import '../datasources/auth_remote_data_source.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this._remote, this._tokens);

  final AuthRemoteDataSource _remote;
  final TokenStore _tokens;

  @override
  Future<void> requestOtp(String phone) => _remote.requestOtp(phone);

  @override
  Future<void> verifyOtp(String phone, String code) async {
    final dto = await _remote.verifyOtp(phone, code);
    await _tokens.save(access: dto.accessToken, refresh: dto.refreshToken);
  }

  @override
  Future<CurrentUser> me() async => (await _remote.me()).toEntity();

  @override
  Future<void> logout() async {
    final refresh = await _tokens.refreshToken();
    if (refresh != null && refresh.isNotEmpty) {
      try {
        await _remote.logout(refresh);
      } catch (_) {
        // best-effort server revoke; local clear below is what matters for the client
      }
    }
    await _tokens.clear();
  }

  @override
  Future<CurrentUser> completeOnboarding({required String name, required String username}) async {
    await _remote.updateProfile(name: name, username: username);
    return me();
  }

  @override
  Future<CurrentUser> updateUsername(String username) async {
    await _remote.updateProfile(username: username);
    return me();
  }

  @override
  Future<bool> checkUsernameAvailable(String username) async =>
      await _remote.usernameAvailability(username) == 'available';

  @override
  Future<CurrentUser> updateDisplayProfile({
    String? name,
    String? headline,
    String? bio,
    List<String>? skills,
    List<String>? languages,
    List<String>? interests,
    String? educationJson,
    String? linksJson,
    String? avatarKey,
    String? coverKey,
    DateTime? dateOfBirth,
  }) async {
    await _remote.updateProfile(
      name: name,
      headline: headline,
      bio: bio,
      skills: skills,
      languages: languages,
      interests: interests,
      educationJson: educationJson,
      linksJson: linksJson,
      avatarKey: avatarKey,
      coverKey: coverKey,
      dateOfBirth: dateOfBirth,
    );
    return me();
  }

  @override
  Future<CurrentUser> updatePrivacy({
    bool? profilePublic,
    bool? showAttended,
    bool? showCertificates,
    bool? showAllies,
    Map<String, String>? sections,
  }) async {
    await _remote.updatePrivacy(
      profilePublic: profilePublic,
      showAttended: showAttended,
      showCertificates: showCertificates,
      showAllies: showAllies,
      sections: sections,
    );
    return me();
  }

  @override
  Future<String> uploadProfileImage({
    required String slot,
    required String contentType,
    required List<int> bytes,
  }) async {
    final presigned = await _remote.presignProfileImage(
      slot: slot,
      contentType: contentType,
      maxBytes: bytes.length,
    );
    await _remote.putPresigned(presigned.url, presigned.headers, contentType, bytes);
    return presigned.key;
  }
}
