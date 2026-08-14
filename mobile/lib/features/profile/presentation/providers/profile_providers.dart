import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../auth/domain/entities/current_user.dart';
import '../../../auth/presentation/providers/auth_providers.dart';

/// The signed-in user's own profile, projected from the real `GET /v1/me` (D-219).
///
/// Every field here is server-backed. An earlier version kept `bio` in a local Hive box because no
/// endpoint served it — that cache was never read by any screen and never sent anywhere, so it was
/// deleted rather than migrated: `/v1/me` now returns the display fields directly.
class ProfileState {
  const ProfileState({
    required this.name,
    required this.phone,
    this.username,
    this.headline,
    this.bio,
    this.skills = const [],
    this.languages = const [],
    this.interests = const [],
    this.educationJson,
    this.linksJson,
    this.avatarKey,
    this.coverKey,
    this.dateOfBirth,
    this.privacy = const ProfilePrivacy(),
  });

  final String name;
  final String phone;
  final String? username;
  final String? headline;
  final String? bio;
  final List<String> skills;

  /// Self-declared, and deliberately distinct from Event DNA — which is derived from what this
  /// person provably did (D-225 keeps claim and proof apart everywhere on the profile).
  final List<String> languages;
  final List<String> interests;

  /// jsonb array; the edit form reads and writes its first entry, which is the one the public
  /// profile renders.
  final String? educationJson;
  final String? linksJson;
  final String? avatarKey;
  final String? coverKey;

  /// Collected during onboarding (D-311) and editable here, so a mis-tapped birth date is not
  /// permanent. Self-declared; never proof of age.
  final DateTime? dateOfBirth;
  final ProfilePrivacy privacy;

  bool get hasUsername => username != null && username!.isNotEmpty;
}

class ProfileController extends Notifier<ProfileState> {
  @override
  ProfileState build() {
    final user = ref.watch(currentUserProvider);
    return ProfileState(
      name: user?.name ?? '',
      phone: user?.phone ?? '',
      username: user?.username,
      headline: user?.headline,
      bio: user?.bio,
      skills: user?.skills ?? const [],
      linksJson: user?.linksJson,
      languages: user?.languages ?? const [],
      interests: user?.interests ?? const [],
      educationJson: user?.educationJson,
      avatarKey: user?.avatarKey,
      coverKey: user?.coverKey,
      dateOfBirth: user?.dateOfBirth,
      privacy: user?.privacy ?? const ProfilePrivacy(),
    );
  }

  Future<bool> isUsernameAvailable(String username) =>
      ref.read(authRepositoryProvider).checkUsernameAvailable(username);

  Future<void> claimUsername(String username) async {
    final user = await ref.read(authRepositoryProvider).updateUsername(username);
    ref.read(currentUserProvider.notifier).state = user;
  }

  /// Saves the self-declared display fields, then republishes the canonical `/v1/me` user so every
  /// screen watching [currentUserProvider] re-renders from the server's copy, not from local state.
  Future<void> saveDisplayProfile({
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
    final user = await ref.read(authRepositoryProvider).updateDisplayProfile(
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
    ref.read(currentUserProvider.notifier).state = user;
  }

  Future<void> savePrivacy({
    bool? profilePublic,
    bool? showAttended,
    bool? showCertificates,
    bool? showAllies,
    Map<String, String>? sections,
  }) async {
    final user = await ref.read(authRepositoryProvider).updatePrivacy(
          profilePublic: profilePublic,
          showAttended: showAttended,
          showCertificates: showCertificates,
          showAllies: showAllies,
          sections: sections,
        );
    ref.read(currentUserProvider.notifier).state = user;
  }

  /// Uploads the bytes and returns the storage key. Persisting it is [saveDisplayProfile]'s job, so
  /// the edit form can show a preview before the user commits the rest of the form.
  Future<String> uploadImage({
    required String slot,
    required String contentType,
    required List<int> bytes,
  }) =>
      ref.read(authRepositoryProvider).uploadProfileImage(
            slot: slot,
            contentType: contentType,
            bytes: bytes,
          );
}

final profileControllerProvider =
    NotifierProvider<ProfileController, ProfileState>(ProfileController.new);
