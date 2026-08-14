import '../repositories/auth_repository.dart';

/// Sends the login OTP for a phone number. The delivery channel belongs to the server (D-281).
class RequestOtp {
  const RequestOtp(this._repo);
  final AuthRepository _repo;

  Future<void> call(String phone) => _repo.requestOtp(phone);
}
