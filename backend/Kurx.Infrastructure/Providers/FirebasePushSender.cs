using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Kurx.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Providers;

public class FirebasePushSender : IPushSender
{
    private readonly FirebaseMessaging? _messaging;
    private readonly ILogger<FirebasePushSender> _log;

    public FirebasePushSender(IConfiguration config, ILogger<FirebasePushSender> log)
    {
        _log = log;
        try
        {
            var creds = config["FIREBASE_CREDENTIALS_JSON"];
            if (!string.IsNullOrWhiteSpace(creds))
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    // GoogleCredential.FromJson/FromStream are both deprecated (advisory, still functional)
                    // in favour of CredentialFactory. Suppressed to satisfy -warnaserror; migrating this FCM
                    // provider to CredentialFactory is a follow-up (it's a real-provider slice for later).
#pragma warning disable CS0618
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromJson(creds)
                    });
#pragma warning restore CS0618
                }
                _messaging = FirebaseMessaging.DefaultInstance;
                _log.LogInformation("Firebase Cloud Messaging initialized successfully.");
            }
            else
            {
                _log.LogWarning("FIREBASE_CREDENTIALS_JSON not configured. Firebase Push Notifications will run in console-fallback mode.");
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to initialize Firebase Admin SDK. Fallback to mock console logging.");
        }
    }

    public async Task SendAsync(string fcmToken, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        if (_messaging is null)
        {
            _log.LogInformation("[push→console-fallback] token={Token} title={Title} body={Body}", fcmToken, title, body);
            return;
        }

        try
        {
            var message = new Message
            {
                Token = fcmToken,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data
            };

            var response = await _messaging.SendAsync(message, ct);
            _log.LogInformation("Push notification sent successfully via FCM: {ResponseId}", response);
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered || ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
        {
            _log.LogWarning(ex, "FCM token unregistered or invalid: {Token}.", fcmToken);
            throw new InvalidFcmTokenException(fcmToken, ex);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "FCM send error for token {Token}", fcmToken);
        }
    }
}

public class InvalidFcmTokenException(string token, Exception inner) : Exception($"Invalid FCM token: {token}", inner)
{
    public string Token { get; } = token;
}
