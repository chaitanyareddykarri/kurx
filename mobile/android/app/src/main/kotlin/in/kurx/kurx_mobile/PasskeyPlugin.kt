package `in`.kurx.kurx_mobile

import androidx.credentials.CreatePublicKeyCredentialRequest
import androidx.credentials.CreatePublicKeyCredentialResponse
import androidx.credentials.CredentialManager
import androidx.credentials.GetCredentialRequest
import androidx.credentials.GetPublicKeyCredentialOption
import androidx.credentials.PublicKeyCredential
import androidx.credentials.exceptions.CreateCredentialCancellationException
import androidx.credentials.exceptions.CreateCredentialException
import androidx.credentials.exceptions.CreateCredentialNoCreateOptionException
import androidx.credentials.exceptions.GetCredentialCancellationException
import androidx.credentials.exceptions.GetCredentialException
import androidx.credentials.exceptions.NoCredentialException
import androidx.credentials.exceptions.domerrors.InvalidStateError
import androidx.credentials.exceptions.publickeycredential.CreatePublicKeyCredentialDomException
import androidx.fragment.app.FragmentActivity
import io.flutter.plugin.common.MethodCall
import io.flutter.plugin.common.MethodChannel
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * Passkeys (WebAuthn/FIDO2) via AndroidX Credential Manager (AM3, D-097).
 *
 * ## Why this plugin is only a JSON shuttle
 *
 * Credential Manager speaks the **W3C WebAuthn JSON serialization** — the exact same shape the
 * backend already produces and consumes via fido2-net-lib (D-086). So this file deliberately does
 * **no** parsing, no base64url handling, and no credential bookkeeping: it hands the server's
 * options JSON straight to the platform and returns the platform's response JSON straight back.
 *
 * That is the whole design point. Any attempt to "help" by decoding fields here would duplicate
 * WebAuthn logic that already exists server-side, and would be a second place for the encoding to
 * drift — historically the most common way a passkey integration breaks.
 *
 * ## Relationship to the device-key rail
 *
 * A passkey is not a parallel identity system. The backend enrols it as an ordinary `TrustedDevice`
 * (D-086), so revocation (D-081), step-up (D-084), risk (D-085) and recovery suspension (D-083)
 * all apply to it unchanged. Nothing about that lives here.
 *
 * ## Deployment prerequisite — this cannot work without it
 *
 * Android verifies the app is allowed to use the relying party's ID via **Digital Asset Links**:
 * `https://<WEBAUTHN_RP_ID>/.well-known/assetlinks.json` must list this app's package name and
 * SHA-256 signing fingerprint. Without it Credential Manager refuses with a DOM error, and an
 * `RP_ID` of `localhost` can never work on a real device. See D-097.
 */
class PasskeyPlugin(private val activity: FragmentActivity) : MethodChannel.MethodCallHandler {

    companion object {
        const val CHANNEL = "kurx/passkey"
    }

    private val credentialManager by lazy { CredentialManager.create(activity) }
    private val scope = CoroutineScope(Dispatchers.Main)

    override fun onMethodCall(call: MethodCall, result: MethodChannel.Result) {
        when (call.method) {
            "isAvailable" -> result.success(true)
            "register" -> withJson(call, result) { json -> register(json, result) }
            "authenticate" -> withJson(call, result) { json -> authenticate(json, result) }
            else -> result.notImplemented()
        }
    }

    private inline fun withJson(call: MethodCall, result: MethodChannel.Result, body: (String) -> Unit) {
        val json = call.argument<String>("requestJson")
        if (json.isNullOrBlank()) {
            result.error("invalid_request", "requestJson is required", null)
            return
        }
        body(json)
    }

    /** Registration ceremony. Returns the platform's `registrationResponseJson` verbatim. */
    private fun register(requestJson: String, result: MethodChannel.Result) {
        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    credentialManager.createCredential(
                        activity,
                        CreatePublicKeyCredentialRequest(requestJson),
                    )
                }
                val created = response as? CreatePublicKeyCredentialResponse
                if (created == null) {
                    result.error("unknown", "Unexpected credential response type", null)
                    return@launch
                }
                result.success(created.registrationResponseJson)
            } catch (e: CreateCredentialCancellationException) {
                // The user dismissed the sheet — a choice, not a fault.
                result.error("user_canceled", e.message, null)
            } catch (e: CreatePublicKeyCredentialDomException) {
                // InvalidStateError specifically means "a passkey for this account already exists on
                // this device" (the server sent it in excludeCredentials). That is a benign,
                // actionable outcome, not a failure.
                val code = if (e.domError is InvalidStateError) "already_registered" else "dom_error"
                result.error(code, e.message, null)
            } catch (e: CreateCredentialNoCreateOptionException) {
                // No provider available — e.g. no Google Password Manager, or no screen lock set.
                result.error("no_provider", e.message, null)
            } catch (e: CreateCredentialException) {
                result.error("unknown", "${e.type}: ${e.message}", null)
            }
        }
    }

    /** Authentication ceremony. Returns the platform's `authenticationResponseJson` verbatim. */
    private fun authenticate(requestJson: String, result: MethodChannel.Result) {
        scope.launch {
            try {
                val response = withContext(Dispatchers.IO) {
                    credentialManager.getCredential(
                        activity,
                        GetCredentialRequest(listOf(GetPublicKeyCredentialOption(requestJson))),
                    )
                }
                val credential = response.credential as? PublicKeyCredential
                if (credential == null) {
                    result.error("unknown", "Unexpected credential type", null)
                    return@launch
                }
                result.success(credential.authenticationResponseJson)
            } catch (e: GetCredentialCancellationException) {
                result.error("user_canceled", e.message, null)
            } catch (e: NoCredentialException) {
                // No passkey on this device for this RP. Distinct from an error: the caller should
                // offer another sign-in method rather than showing a failure.
                result.error("no_credential", e.message, null)
            } catch (e: GetCredentialException) {
                result.error("unknown", "${e.type}: ${e.message}", null)
            }
        }
    }
}
