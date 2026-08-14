import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/auth/data/models/current_user_dto.dart';

void main() {
  test('CurrentUserDto maps snake_case /v1/me and needs_onboarding', () {
    final dto = CurrentUserDto.fromJson({
      'id': '7ee15dff-12f8-490c-93fb-75a4f2266270',
      'phone': '919876543210',
      'name': '',
      'username': null,
      'email': null,
      'needs_onboarding': true,
    });

    expect(dto.needsOnboarding, isTrue);

    final user = dto.toEntity();
    expect(user.id, '7ee15dff-12f8-490c-93fb-75a4f2266270');
    expect(user.phone, '919876543210');
    expect(user.needsOnboarding, isTrue);
  });

  test('CurrentUserDto maps the display fields and privacy block (D-219)', () {
    final dto = CurrentUserDto.fromJson({
      'id': '7ee15dff-12f8-490c-93fb-75a4f2266270',
      'phone': '919876543210',
      'name': 'Asha',
      'username': 'asha',
      'email': null,
      'needs_onboarding': false,
      'headline': 'Event organizer',
      'bio': 'Builds communities.',
      'skills': ['flutter', 'public speaking'],
      'links_json': '{"github": "https://github.com/asha"}',
      'avatar_key': 'users/7ee15dff/avatar/abc',
      'cover_key': 'users/7ee15dff/cover/def',
      'privacy': {
        'profile_public': false,
        'show_attended': true,
        'show_certificates': false,
        'show_allies': true,
      },
    });

    final user = dto.toEntity();
    expect(user.headline, 'Event organizer');
    expect(user.bio, 'Builds communities.');
    expect(user.skills, ['flutter', 'public speaking']);
    expect(user.linksJson, contains('github'));
    expect(user.avatarKey, 'users/7ee15dff/avatar/abc');
    expect(user.coverKey, 'users/7ee15dff/cover/def');
    expect(user.privacy.profilePublic, isFalse);
    expect(user.privacy.showAttended, isTrue);
    expect(user.privacy.showCertificates, isFalse);
    expect(user.privacy.showAllies, isTrue);
  });

  /// An older backend omits the whole block. Defaults must match the server's column defaults, so a
  /// missing block is indistinguishable from an untouched account rather than silently reading as
  /// "everything hidden".
  test('a missing privacy block falls back to the server column defaults', () {
    final dto = CurrentUserDto.fromJson({
      'id': '7ee15dff-12f8-490c-93fb-75a4f2266270',
      'phone': '919876543210',
      'name': 'Asha',
      'needs_onboarding': false,
    });

    final user = dto.toEntity();
    expect(user.privacy.profilePublic, isTrue);
    expect(user.privacy.showAttended, isFalse);
    expect(user.privacy.showCertificates, isTrue);
    expect(user.privacy.showAllies, isTrue);
    expect(user.skills, isEmpty);
    expect(user.headline, isNull);
  });
}
