using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kurx.Tests;

/// <summary>A minimal WebAuthn authenticator, in-process, for testing the passkey rail (AM3) end-to-end.
///
/// Without this the cryptographic happy path could not be covered at all — a real passkey needs browser +
/// platform authenticator hardware. It implements just enough of CTAP/WebAuthn to be accepted by
/// fido2-net-lib: an ES256 key pair, <c>none</c> attestation (no attestation certificate to fake), and
/// correctly framed <c>authenticatorData</c> + <c>clientDataJSON</c>. CBOR comes from the BCL
/// (<see cref="CborWriter"/>), so this needs no test-only package.</summary>
public class SoftwareAuthenticator
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _credentialId = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _aaguid = new byte[16];      // all-zero is what a "none"-attestation platform key reports
    private uint _signCount;

    public string RpId { get; init; } = "localhost";
    public string Origin { get; init; } = "https://localhost";

    /// <summary>Lets a test simulate a cloned authenticator by rewinding the counter.</summary>
    public void ForceSignCount(uint value) => _signCount = value;

    /// <summary>Builds the <c>navigator.credentials.create()</c> response for the given challenge.</summary>
    public JsonElement AttestationResponse(string challengeBase64Url)
    {
        var clientDataJson = ClientData("webauthn.create", challengeBase64Url);
        var authData = AuthenticatorData(includeAttestedCredential: true);

        // attestationObject = CBOR { fmt: "none", attStmt: {}, authData: <bytes> }
        var writer = new CborWriter();
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString("none");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(authData);
        writer.WriteEndMap();

        return Json(new
        {
            id = Base64Url(_credentialId),
            rawId = Base64Url(_credentialId),
            type = "public-key",
            extensions = new { },
            response = new
            {
                attestationObject = Base64Url(writer.Encode()),
                clientDataJSON = Base64Url(clientDataJson),
            },
        });
    }

    /// <summary>Builds the <c>navigator.credentials.get()</c> response, signing over
    /// <c>authenticatorData || SHA-256(clientDataJSON)</c> exactly as a real authenticator does.</summary>
    public JsonElement AssertionResponse(string challengeBase64Url, Guid userId)
    {
        _signCount++;
        var clientDataJson = ClientData("webauthn.get", challengeBase64Url);
        var authData = AuthenticatorData(includeAttestedCredential: false);

        var signedPayload = authData.Concat(SHA256.HashData(clientDataJson)).ToArray();
        var signature = _key.SignData(signedPayload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return Json(new
        {
            id = Base64Url(_credentialId),
            rawId = Base64Url(_credentialId),
            type = "public-key",
            extensions = new { },
            response = new
            {
                authenticatorData = Base64Url(authData),
                clientDataJSON = Base64Url(clientDataJson),
                signature = Base64Url(signature),
                userHandle = Base64Url(userId.ToByteArray()),
            },
        });
    }

    private byte[] ClientData(string type, string challengeBase64Url) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        type,
        challenge = challengeBase64Url,
        origin = Origin,
        crossOrigin = false,
    });

    /// <summary>rpIdHash ‖ flags ‖ signCount ‖ [aaguid ‖ credIdLen ‖ credId ‖ COSE key].</summary>
    private byte[] AuthenticatorData(bool includeAttestedCredential)
    {
        var data = new List<byte>();
        data.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(RpId)));

        // UP (user present) | UV (user verified), plus AT (attested credential data) during registration.
        byte flags = 0x01 | 0x04;
        if (includeAttestedCredential) flags |= 0x40;
        data.Add(flags);

        var counter = BitConverter.GetBytes(_signCount);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);     // WebAuthn counters are big-endian
        data.AddRange(counter);

        if (includeAttestedCredential)
        {
            data.AddRange(_aaguid);
            var idLength = BitConverter.GetBytes((ushort)_credentialId.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(idLength);
            data.AddRange(idLength);
            data.AddRange(_credentialId);
            data.AddRange(CoseKey());
        }

        return data.ToArray();
    }

    /// <summary>The public key as a COSE_Key map: kty=EC2, alg=ES256, crv=P-256, x, y.</summary>
    private byte[] CoseKey()
    {
        var p = _key.ExportParameters(includePrivateParameters: false);
        var writer = new CborWriter();
        writer.WriteStartMap(5);
        writer.WriteInt32(1); writer.WriteInt32(2);       // kty: EC2
        writer.WriteInt32(3); writer.WriteInt32(-7);      // alg: ES256
        writer.WriteInt32(-1); writer.WriteInt32(1);      // crv: P-256
        writer.WriteInt32(-2); writer.WriteByteString(p.Q.X!);
        writer.WriteInt32(-3); writer.WriteByteString(p.Q.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static JsonElement Json(object value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
