package `in`.kurx.kurx_mobile

import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyInfo
import android.security.keystore.KeyPermanentlyInvalidatedException
import android.security.keystore.KeyProperties
import android.util.Base64
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.fragment.app.FragmentActivity
import io.flutter.plugin.common.MethodCall
import io.flutter.plugin.common.MethodChannel
import java.security.KeyFactory
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.Signature
import java.security.spec.ECGenParameterSpec
import java.util.concurrent.Executor
import java.util.concurrent.Executors

/**
 * Hardware-backed ES256 device identity key (AM2/AM4, D-091).
 *
 * The private key is generated **inside** AndroidKeyStore and is never exported — that is the whole
 * security property the trusted-device rail rests on (D-081: a stolen refresh token is inert without
 * a signature from this key). Nothing in this file ever handles raw private key bytes, because it
 * cannot: `KeyStore.PrivateKey` is an opaque handle to key material held by the TEE/StrongBox.
 *
 * Signing requires user authentication, so a signature is proof a human was present — not merely
 * that the app was running. That is what makes "approve this sign-in" meaningful.
 */
class DeviceKeyPlugin(private val activity: FragmentActivity) : MethodChannel.MethodCallHandler {

    companion object {
        const val CHANNEL = "kurx/device_key"
        private const val KEY_ALIAS = "kurx_device_identity_key"
        private const val KEYSTORE = "AndroidKeyStore"

        /** Signature validity window. 0 = every signature needs a fresh prompt (no grace period). */
        private const val AUTH_VALIDITY_SECONDS = 0
    }

    private val executor: Executor = Executors.newSingleThreadExecutor()

    private fun keyStore(): KeyStore = KeyStore.getInstance(KEYSTORE).apply { load(null) }

    override fun onMethodCall(call: MethodCall, result: MethodChannel.Result) {
        when (call.method) {
            "hasKey" -> result.success(keyStore().containsAlias(KEY_ALIAS))
            "createKey" -> createKey(result)
            "publicKeySpki" -> publicKeySpki(result)
            "sign" -> sign(call.argument<String>("message"), result)
            "deleteKey" -> deleteKey(result)
            "securityInfo" -> securityInfo(result)
            else -> result.notImplemented()
        }
    }

    /**
     * Creates the key pair. Tries StrongBox (a discrete security chip) first and falls back to the
     * TEE, because StrongBox is absent on most devices and on every emulator — a hard requirement
     * would lock out the majority of real users for no gain, since TEE-backed is already
     * non-exportable.
     */
    private fun createKey(result: MethodChannel.Result) {
        val biometricStatus = canAuthenticate()
        if (biometricStatus != BiometricManager.BIOMETRIC_SUCCESS) {
            // Creating an auth-required key with nothing enrolled produces a key that can never be
            // used. Fail loudly here rather than at first signature, when the user is mid-login.
            result.error(biometricErrorCode(biometricStatus), "No usable device authentication enrolled", null)
            return
        }

        try {
            deleteKeyIfPresent()
            generate(useStrongBox = supportsStrongBox())
        } catch (e: Exception) {
            // StrongBox can advertise support and still refuse the spec on some firmware.
            try {
                deleteKeyIfPresent()
                generate(useStrongBox = false)
            } catch (fallback: Exception) {
                result.error("unknown", "Key generation failed: ${fallback.message}", null)
                return
            }
        }

        publicKeySpki(result)
    }

    private fun generate(useStrongBox: Boolean) {
        val generator = KeyPairGenerator.getInstance(KeyProperties.KEY_ALGORITHM_EC, KEYSTORE)
        val spec = KeyGenParameterSpec.Builder(KEY_ALIAS, KeyProperties.PURPOSE_SIGN)
            .setAlgorithmParameterSpec(ECGenParameterSpec("secp256r1"))   // P-256 => ES256
            .setDigests(KeyProperties.DIGEST_SHA256)
            .setUserAuthenticationRequired(true)
            .apply {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                    // Biometric OR device credential (PIN/pattern/password): refusing the credential
                    // fallback would strand users whose biometric hardware fails or is unenrolled.
                    setUserAuthenticationParameters(
                        AUTH_VALIDITY_SECONDS,
                        KeyProperties.AUTH_BIOMETRIC_STRONG or KeyProperties.AUTH_DEVICE_CREDENTIAL
                    )
                } else {
                    @Suppress("DEPRECATION")
                    setUserAuthenticationValidityDurationSeconds(AUTH_VALIDITY_SECONDS)
                }
                // Enrolling a new biometric invalidates the key: the person who can unlock the phone
                // changed, so the old proof-of-possession must not survive (matches iOS
                // biometryCurrentSet). The app treats this as "re-enroll this device", not an error.
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
                    setInvalidatedByBiometricEnrollment(true)
                }
                if (useStrongBox && Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                    setIsStrongBoxBacked(true)
                }
            }
            .build()

        generator.initialize(spec)
        generator.generateKeyPair()
    }

    private fun publicKeySpki(result: MethodChannel.Result) {
        val entry = keyStore().getCertificate(KEY_ALIAS)
        if (entry == null) {
            result.success(null)
            return
        }
        // getEncoded() on a public key is already SubjectPublicKeyInfo DER — exactly what the
        // backend's DeviceSignatures.VerifyEs256 imports.
        val spki = entry.publicKey.encoded
        result.success(Base64.encodeToString(spki, Base64.NO_WRAP))
    }

    /**
     * Signs the challenge nonce, prompting for biometrics/credential.
     *
     * The [Signature] is handed to BiometricPrompt inside a CryptoObject, so the keystore only
     * releases the key for *this* operation after *this* authentication. Authenticating and then
     * signing separately would leave a window where any code could sign.
     */
    private fun sign(message: String?, result: MethodChannel.Result) {
        if (message.isNullOrEmpty()) {
            result.error("unknown", "message is required", null)
            return
        }

        val signature: Signature
        try {
            val entry = keyStore().getEntry(KEY_ALIAS, null) as? KeyStore.PrivateKeyEntry
            if (entry == null) {
                result.error("not_enrolled", "No device key on this device", null)
                return
            }
            signature = Signature.getInstance("SHA256withECDSA").apply { initSign(entry.privateKey) }
        } catch (e: KeyPermanentlyInvalidatedException) {
            // Biometric enrollment changed since the key was made. Recoverable, but only by
            // re-enrolling the device — the Dart layer maps this to that flow.
            result.error("key_invalidated", "Device security changed; re-enroll this device", null)
            return
        } catch (e: Exception) {
            result.error("unknown", e.message, null)
            return
        }

        val prompt = BiometricPrompt(
            activity,
            executor,
            object : BiometricPrompt.AuthenticationCallback() {
                override fun onAuthenticationSucceeded(authResult: BiometricPrompt.AuthenticationResult) {
                    try {
                        val signed = authResult.cryptoObject?.signature
                            ?: run {
                                activity.runOnUiThread { result.error("unknown", "No signature in crypto object", null) }
                                return
                            }
                        signed.update(message.toByteArray(Charsets.UTF_8))
                        val der = signed.sign()   // DER-encoded; the backend accepts DER or raw r‖s
                        activity.runOnUiThread {
                            result.success(Base64.encodeToString(der, Base64.NO_WRAP))
                        }
                    } catch (e: Exception) {
                        activity.runOnUiThread { result.error("unknown", e.message, null) }
                    }
                }

                override fun onAuthenticationError(errorCode: Int, errString: CharSequence) {
                    val code = when (errorCode) {
                        BiometricPrompt.ERROR_USER_CANCELED,
                        BiometricPrompt.ERROR_NEGATIVE_BUTTON,
                        BiometricPrompt.ERROR_CANCELED -> "user_canceled"
                        BiometricPrompt.ERROR_NO_BIOMETRICS,
                        BiometricPrompt.ERROR_NO_DEVICE_CREDENTIAL -> "not_enrolled"
                        BiometricPrompt.ERROR_HW_NOT_PRESENT,
                        BiometricPrompt.ERROR_HW_UNAVAILABLE -> "unavailable"
                        else -> "unknown"
                    }
                    activity.runOnUiThread { result.error(code, errString.toString(), null) }
                }

                // Not terminal: a rejected fingerprint lets the user try again on the same prompt.
                override fun onAuthenticationFailed() = Unit
            }
        )

        val info = BiometricPrompt.PromptInfo.Builder()
            .setTitle("Confirm it's you")
            .setSubtitle("Approve this request for your Kurx account")
            .setAllowedAuthenticators(
                BiometricManager.Authenticators.BIOMETRIC_STRONG or
                    BiometricManager.Authenticators.DEVICE_CREDENTIAL
            )
            .build()

        activity.runOnUiThread {
            prompt.authenticate(info, BiometricPrompt.CryptoObject(signature))
        }
    }

    private fun deleteKey(result: MethodChannel.Result) {
        deleteKeyIfPresent()
        result.success(null)
    }

    private fun deleteKeyIfPresent() {
        val ks = keyStore()
        if (ks.containsAlias(KEY_ALIAS)) ks.deleteEntry(KEY_ALIAS)
    }

    /**
     * Reports what this device actually provides, so the app can tell the user the truth rather
     * than claiming hardware protection it may not have. Also what the integration tests assert on.
     */
    private fun securityInfo(result: MethodChannel.Result) {
        val info = mutableMapOf<String, Any?>(
            "strongBoxSupported" to supportsStrongBox(),
            "biometricStatus" to canAuthenticate(),
            "apiLevel" to Build.VERSION.SDK_INT,
            "hasKey" to keyStore().containsAlias(KEY_ALIAS)
        )

        // Whether the key really landed in secure hardware, read back from the key itself rather
        // than assumed from the request.
        try {
            val entry = keyStore().getEntry(KEY_ALIAS, null) as? KeyStore.PrivateKeyEntry
            if (entry != null) {
                val factory = KeyFactory.getInstance(entry.privateKey.algorithm, KEYSTORE)
                val keyInfo = factory.getKeySpec(entry.privateKey, KeyInfo::class.java)
                info["insideSecureHardware"] =
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                        keyInfo.securityLevel >= KeyProperties.SECURITY_LEVEL_TRUSTED_ENVIRONMENT
                    } else {
                        @Suppress("DEPRECATION") keyInfo.isInsideSecureHardware
                    }
                info["userAuthenticationRequired"] = keyInfo.isUserAuthenticationRequired
                info["invalidatedByBiometricEnrollment"] =
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
                        keyInfo.isInvalidatedByBiometricEnrollment
                    } else null
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                    info["securityLevel"] = keyInfo.securityLevel
                }
            }
        } catch (e: Exception) {
            info["inspectionError"] = e.message
        }

        result.success(info)
    }

    private fun supportsStrongBox(): Boolean =
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.P &&
            activity.packageManager.hasSystemFeature("android.hardware.strongbox_keystore")

    private fun canAuthenticate(): Int = BiometricManager.from(activity).canAuthenticate(
        BiometricManager.Authenticators.BIOMETRIC_STRONG or
            BiometricManager.Authenticators.DEVICE_CREDENTIAL
    )

    private fun biometricErrorCode(status: Int): String = when (status) {
        BiometricManager.BIOMETRIC_ERROR_NONE_ENROLLED -> "not_enrolled"
        BiometricManager.BIOMETRIC_ERROR_NO_HARDWARE,
        BiometricManager.BIOMETRIC_ERROR_HW_UNAVAILABLE -> "unavailable"
        else -> "unavailable"
    }
}
