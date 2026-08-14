import '../entities/current_user.dart';
import '../repositories/auth_repository.dart';

/// Verifies the OTP (which persists the tokens) and then loads the current user,
/// so the caller gets the onboarding signal in one step.
class CompleteOtpLogin {
  const CompleteOtpLogin(this._repo);
  final AuthRepository _repo;

  Future<CurrentUser> call({required String phone, required String code}) async {
    await _repo.verifyOtp(phone, code);
    return _repo.me();
  }
}
