using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Auth;

/// <summary>
/// The one place that decides which channel carries a one-time code (D-281).
/// </summary>
/// <remarks>
/// <para>Every call site used to name a channel itself, and every one of them named
/// <see cref="OtpChannel.WhatsApp"/> — so <see cref="OtpChannel.Sms"/> was selected by nothing and
/// <c>SnsSmsProvider</c> was unreachable code. Centralising the choice is what makes the SMS rail
/// reachable and what stops the next caller re-deciding it differently.</para>
///
/// <para>WhatsApp is deliberately absent. It remains a fully supported channel — the provider seam, the
/// inbound webhook and the message log all stay — but it is never selected for authentication: a WhatsApp
/// account is portable across devices and recoverable through a takeover chain Kurx does not control, which
/// makes it a weaker credential path than SMS.</para>
///
/// <para>Not a service and not injected: this is a pure function over a closed enum with no state and no
/// configuration. An interface here would buy nothing and would have to be mocked in every auth test.</para>
/// </remarks>
public static class OtpChannelPolicy
{
    /// <summary>The channel a code for this purpose is delivered over.</summary>
    public static OtpChannel For(OtpPurpose purpose) => purpose switch
    {
        // Email-addressed purposes. EmailLogin is the second factor (D-282); EmailVerification proves
        // ownership of an address and predates it.
        OtpPurpose.EmailLogin or OtpPurpose.EmailVerification => OtpChannel.Email,
        // Everything else is addressed by phone: registration, login, phone verification, recovery,
        // password reset, device enrollment, step-up.
        _ => OtpChannel.Sms,
    };
}
