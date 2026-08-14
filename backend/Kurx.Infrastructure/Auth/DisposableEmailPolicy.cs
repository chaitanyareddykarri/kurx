using Microsoft.Extensions.Configuration;

namespace Kurx.Infrastructure.Auth;

/// <summary>Recognises disposable/throwaway email domains, so a verified address of that kind can be
/// <b>recorded as a risk signal</b> rather than trusted silently.
///
/// <para><b>This is detection, not enforcement.</b> It never refuses a registration and never blocks a
/// verification. Email is not an authentication factor on this platform — phone OTP is — so a throwaway
/// address costs far less here than it would on an email-first product, and blocking one at signup would
/// punish exactly the users least likely to be committing fraud: students on temporary institutional
/// addresses, and privacy-conscious attendees. The risk that actually matters is organising paid public
/// events, and that path is already gated on identity + PAN + bank + fraud-clear (D-307). So this feeds
/// the existing score and lets that gate decide, instead of inventing a second verdict.</para>
///
/// <para><b>The list is configuration, never source.</b> Disposable-domain lists change continuously and
/// carry false positives; baking one into a compiled assembly means a wrong entry can only be corrected by
/// a deploy. <c>DISPOSABLE_EMAIL_DOMAINS</c> is a comma-separated list, absent by default — which makes
/// the whole feature a no-op until an operator opts in, and is the correct steady state for an install
/// that has not decided its policy yet.</para></summary>
public sealed class DisposableEmailPolicy
{
    public const string ConfigKey = "DISPOSABLE_EMAIL_DOMAINS";

    /// <summary>Score attached to the signal. Deliberately far below
    /// <see cref="Trust.FraudService.HighRiskThreshold"/> (100): this phase exists to measure how much of
    /// this traffic is real before anything is denied on it. Raising it to a blocking weight is a separate
    /// decision that should be made against observed volume, not in advance.</summary>
    public const int SignalScore = 10;

    private readonly HashSet<string> _domains;

    public DisposableEmailPolicy(IConfiguration config)
    {
        _domains = (config[ConfigKey] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.TrimStart('@').ToLowerInvariant())
            .Where(d => d.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>True when no domain is configured — the caller can then skip the check entirely.</summary>
    public bool IsEmpty => _domains.Count == 0;

    /// <summary>Whether this address's domain is on the configured list.
    ///
    /// <para><b>Subdomains match, deliberately.</b> A configured <c>mailinator.com</c> also matches
    /// <c>team.mailinator.com</c>, because disposable providers hand out per-user subdomains and matching
    /// only the exact host would be defeated by the very feature that makes them disposable. The match is
    /// anchored on a dot boundary so <c>notmailinator.com</c> — a different registrable domain — does not
    /// match.</para>
    ///
    /// <para>Case is irrelevant on both sides: the address is lowercased by the caller's normaliser and the
    /// set is ordinal-ignore-case, so <c>USER@Mailinator.COM</c> is recognised.</para></summary>
    public bool IsDisposable(string? email)
    {
        if (_domains.Count == 0 || string.IsNullOrWhiteSpace(email)) return false;

        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1) return false;
        var host = email[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0) return false;

        if (_domains.Contains(host)) return true;
        // Walk the parents: team.mailinator.com → mailinator.com → com. Cheaper and more predictable than
        // a suffix scan over the whole set, and it can only ever match on a label boundary.
        for (var i = host.IndexOf('.'); i > 0 && i < host.Length - 1; i = host.IndexOf('.', i + 1))
            if (_domains.Contains(host[(i + 1)..]))
                return true;

        return false;
    }
}
