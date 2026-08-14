import Foundation
import LocalAuthentication
import Security

/// Hardware-backed ES256 device identity key on iOS (AM2/AM4, D-092) — the Secure Enclave twin of
/// `DeviceKeyPlugin.kt`.
///
/// ⚠️ **UNVERIFIED — NOT COMPILED.** This file was written on a Windows host with no macOS or Xcode
/// available, so it has never been compiled, linked, or run. Treat it as a reviewed design, not as
/// working code, until it is built on a Mac and exercised on a real device (the Secure Enclave is
/// absent from the iOS simulator, so a simulator run proves nothing either).
///
/// ## Design
///
/// The private key is generated with `kSecAttrTokenIDSecureEnclave`, so it is created inside the
/// Enclave and the app only ever holds an opaque `SecKey` reference — the raw private key is not
/// representable in process memory. This is the same property AndroidKeyStore gives, and it is what
/// makes a stolen refresh token inert (D-081).
///
/// Access control is `.biometryCurrentSet` + `.privateKeyUsage`:
/// * `privateKeyUsage` — the key may only be used for signing, never exported.
/// * `biometryCurrentSet` — **enrolling or removing a Face/Touch ID identity destroys the key.**
///   That is deliberate and mirrors Android's `setInvalidatedByBiometricEnrollment(true)`: if the
///   set of people who can unlock the phone changes, the old proof-of-possession must not survive.
///   The app surfaces this as "re-enroll this device", which is a normal lifecycle event.
///
/// Note the deliberate absence of a device-passcode fallback: `.biometryCurrentSet` (not
/// `.userPresence`) means a thief who knows the passcode still cannot sign. Android accepts
/// `DEVICE_CREDENTIAL` because Android users may have no biometric hardware at all; on iOS every
/// Secure-Enclave-capable device has biometrics, so the stricter option costs nothing.
@objc class DeviceKeyPlugin: NSObject {

    static let channelName = "kurx/device_key"

    /// Keychain tag identifying this app's single device key.
    private let keyTag = "in.kurx.kurx_mobile.device_identity_key".data(using: .utf8)!

    // MARK: - Channel entry point

    @objc func handle(_ method: String, arguments: Any?, result: @escaping (Any?) -> Void,
                      error: @escaping (String, String?) -> Void) {
        switch method {
        case "hasKey":
            result(loadKey() != nil)
        case "createKey":
            createKey(result: result, error: error)
        case "publicKeySpki":
            publicKeySpki(result: result, error: error)
        case "sign":
            guard let args = arguments as? [String: Any], let message = args["message"] as? String else {
                error("unknown", "message is required")
                return
            }
            sign(message: message, result: result, error: error)
        case "deleteKey":
            deleteKey()
            result(nil)
        case "securityInfo":
            result(securityInfo())
        default:
            error("unimplemented", "Unknown method \(method)")
        }
    }

    // MARK: - Key lifecycle

    private func createKey(result: @escaping (Any?) -> Void, error: @escaping (String, String?) -> Void) {
        guard isSecureEnclaveAvailable() else {
            error("unavailable", "Secure Enclave is not available on this device")
            return
        }

        var biometryError: NSError?
        let context = LAContext()
        // Fail up front rather than at first signature: an auth-required key created with nothing
        // enrolled can never be used, and the user would hit that mid-login.
        guard context.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &biometryError) else {
            error("not_enrolled", biometryError?.localizedDescription ?? "No biometrics enrolled")
            return
        }

        deleteKey()   // replacing means the caller must re-enroll; never leave two keys behind

        var accessError: Unmanaged<CFError>?
        guard let access = SecAccessControlCreateWithFlags(
            kCFAllocatorDefault,
            kSecAttrAccessibleWhenUnlockedThisDeviceOnly,   // never restored onto a different handset
            [.privateKeyUsage, .biometryCurrentSet],
            &accessError
        ) else {
            error("unknown", "Could not create access control: \(describe(accessError))")
            return
        }

        let attributes: [String: Any] = [
            kSecAttrKeyType as String: kSecAttrKeyTypeECSECPrimeRandom,   // P-256 => ES256
            kSecAttrKeySizeInBits as String: 256,
            kSecAttrTokenID as String: kSecAttrTokenIDSecureEnclave,
            kSecPrivateKeyAttrs as String: [
                kSecAttrIsPermanent as String: true,
                kSecAttrApplicationTag as String: keyTag,
                kSecAttrAccessControl as String: access
            ]
        ]

        var createError: Unmanaged<CFError>?
        guard SecKeyCreateRandomKey(attributes as CFDictionary, &createError) != nil else {
            error("unknown", "Key generation failed: \(describe(createError))")
            return
        }

        publicKeySpki(result: result, error: error)
    }

    private func loadKey() -> SecKey? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassKey,
            kSecAttrApplicationTag as String: keyTag,
            kSecAttrKeyType as String: kSecAttrKeyTypeECSECPrimeRandom,
            kSecReturnRef as String: true
        ]
        var item: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess else { return nil }
        return (item as! SecKey?)
    }

    private func deleteKey() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassKey,
            kSecAttrApplicationTag as String: keyTag
        ]
        SecItemDelete(query as CFDictionary)
    }

    // MARK: - Public key

    /// Returns base64 **SubjectPublicKeyInfo**, matching what the backend's
    /// `DeviceSignatures.VerifyEs256` imports and what Android's `publicKey.encoded` produces.
    ///
    /// `SecKeyCopyExternalRepresentation` yields the bare X9.63 point (`04 || X || Y`), *not* SPKI,
    /// so the DER header identifying `id-ecPublicKey` + `prime256v1` is prepended here. Sending the
    /// raw point instead is the single most likely integration bug on this platform: the server
    /// rejects it with an opaque parse failure.
    private func publicKeySpki(result: @escaping (Any?) -> Void, error: @escaping (String, String?) -> Void) {
        guard let key = loadKey(), let publicKey = SecKeyCopyPublicKey(key) else {
            result(nil)
            return
        }

        var exportError: Unmanaged<CFError>?
        guard let raw = SecKeyCopyExternalRepresentation(publicKey, &exportError) as Data? else {
            error("unknown", "Could not export public key: \(describe(exportError))")
            return
        }

        // SEQUENCE { SEQUENCE { id-ecPublicKey, prime256v1 }, BIT STRING { 00 || point } }
        let header: [UInt8] = [
            0x30, 0x59,
            0x30, 0x13,
            0x06, 0x07, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01,        // id-ecPublicKey
            0x06, 0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07,  // prime256v1
            0x03, 0x42, 0x00
        ]
        var spki = Data(header)
        spki.append(raw)
        result(spki.base64EncodedString())
    }

    // MARK: - Signing

    /// Signs the challenge nonce. iOS shows the biometric prompt implicitly when the key is used,
    /// so there is no separate "authenticate then sign" window in which the key sits unlocked.
    private func sign(message: String, result: @escaping (Any?) -> Void,
                      error: @escaping (String, String?) -> Void) {
        guard let key = loadKey() else {
            error("not_enrolled", "No device key on this device")
            return
        }
        guard let data = message.data(using: .utf8) else {
            error("unknown", "Message is not valid UTF-8")
            return
        }

        // Signing blocks on the biometric prompt; keep it off the platform-channel thread.
        DispatchQueue.global(qos: .userInitiated).async {
            var signError: Unmanaged<CFError>?
            let signature = SecKeyCreateSignature(
                key,
                .ecdsaSignatureMessageX962SHA256,   // DER-encoded ECDSA; the backend accepts DER
                data as CFData,
                &signError
            ) as Data?

            DispatchQueue.main.async {
                guard let signature else {
                    let code = self.mapSigningError(signError)
                    error(code, self.describe(signError))
                    return
                }
                result(signature.base64EncodedString())
            }
        }
    }

    /// Maps Security-framework failures onto the codes the Dart layer branches on.
    private func mapSigningError(_ error: Unmanaged<CFError>?) -> String {
        guard let err = error?.takeUnretainedValue() else { return "unknown" }
        switch CFErrorGetCode(err) {
        case Int(errSecUserCanceled), Int(errSecAuthFailed):
            return "user_canceled"
        // The key was destroyed because the enrolled biometric set changed.
        case Int(errSecItemNotFound), Int(errSecInvalidKeyRef):
            return "key_invalidated"
        default:
            return "unknown"
        }
    }

    // MARK: - Diagnostics

    private func securityInfo() -> [String: Any?] {
        var biometryError: NSError?
        let context = LAContext()
        let canEvaluate = context.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &biometryError)

        return [
            "secureEnclaveAvailable": isSecureEnclaveAvailable(),
            // 0 mirrors Android's BIOMETRIC_SUCCESS so the Dart layer reads one scale.
            "biometricStatus": canEvaluate ? 0 : 11,
            "biometryType": biometryTypeName(context),
            "hasKey": loadKey() != nil,
            // On iOS a key created with kSecAttrTokenIDSecureEnclave is in hardware by construction —
            // there is no partial mode to detect, unlike Android's TEE-vs-software distinction.
            "insideSecureHardware": loadKey() != nil,
            "userAuthenticationRequired": loadKey() != nil,
            "invalidatedByBiometricEnrollment": loadKey() != nil
        ]
    }

    private func isSecureEnclaveAvailable() -> Bool {
        #if targetEnvironment(simulator)
        // The simulator has no Secure Enclave; claiming otherwise would let a simulator run
        // masquerade as hardware validation.
        return false
        #else
        return true
        #endif
    }

    private func biometryTypeName(_ context: LAContext) -> String {
        switch context.biometryType {
        case .faceID: return "faceID"
        case .touchID: return "touchID"
        case .opticID: return "opticID"
        default: return "none"
        }
    }

    private func describe(_ error: Unmanaged<CFError>?) -> String {
        guard let err = error?.takeUnretainedValue() else { return "unknown error" }
        return CFErrorCopyDescription(err) as String? ?? "unknown error"
    }
}
