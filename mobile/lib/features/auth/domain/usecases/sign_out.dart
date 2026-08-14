import '../repositories/auth_repository.dart';

/// Revokes the refresh token server-side (best effort) and clears local tokens.
class SignOut {
  const SignOut(this._repo);
  final AuthRepository _repo;

  Future<void> call() => _repo.logout();
}
