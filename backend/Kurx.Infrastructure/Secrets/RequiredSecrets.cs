using Kurx.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kurx.Infrastructure.Secrets;

/// <summary>Startup validation that every secret the platform cannot run without is actually
/// readable (AM10, D-101a).
///
/// <para><b>Why at startup and not on first use.</b> A missing secret discovered on first use is an
/// intermittent, user-visible failure at an arbitrary time — typically the first login after a
/// deploy. Discovered at startup it is a boot failure with the secret's name in the message, which
/// a deployment pipeline catches before any traffic is routed. Fail closed, fail early, fail
/// loudly.</para>
///
/// <para>This also proves the <i>provider</i> works, not just that the values exist: a
/// misconfigured AWS region or a missing IAM permission surfaces here rather than mid-incident.</para></summary>
public static class RequiredSecrets
{
    /// <summary>Secrets with no safe default. Deliberately short — anything with a working default
    /// belongs in configuration, not here.
    ///
    /// <para>OTP_PEPPER is here because its failure mode is silent (D-115): without it, OTP hashes are
    /// computed under a guessable pepper and everything still appears to work. A secret whose absence
    /// degrades security without degrading behaviour has to be caught at startup — nothing later will
    /// notice.</para></summary>
    public static readonly string[] Names = ["JWT_SECRET", "OTP_PEPPER"];

    public static async Task ValidateAsync(ISecretProvider secrets, ILogger logger,
        bool isProduction, CancellationToken ct = default)
    {
        var missing = new List<string>();
        var present = new List<(string Name, string Value)>();

        foreach (var name in Names)
        {
            try
            {
                var value = await secrets.GetAsync(name, ct);
                if (string.IsNullOrWhiteSpace(value)) missing.Add(name);
                else present.Add((name, value));
            }
            catch (Exception ex)
            {
                // A provider that throws is itself a failure: the secret may exist but be
                // unreachable, which in production is indistinguishable from absent.
                logger.LogCritical(ex, "Secret provider failed while reading {SecretName}", name);
                missing.Add(name);
            }
        }

        // Strength is checked OUTSIDE the loop's try, deliberately: RequireStrongProductionSecret
        // signals a weak value by throwing, and the catch above would have swallowed it into
        // `missing` — reporting "secret unavailable" for a secret that is present but unusable.
        //
        // Present is not the same as safe. JWT_SECRET and TICKET_HMAC_SECRET are already
        // strength-checked in JwtOptions.FromSecret, but OTP_PEPPER only had a presence check, so
        // the committed placeholder booted a Production host clean. That is the one secret whose
        // misconfiguration nothing downstream ever surfaces: OTP hashes computed under a guessable
        // pepper behave exactly like correct ones (D-115).
        if (isProduction)
            foreach (var (name, value) in present)
                Configuration.SecretValidation.RequireStrongProductionSecret(name, value);

        if (missing.Count == 0)
        {
            logger.LogInformation("All {Count} required secrets are available", Names.Length);
            return;
        }

        var message = $"Required secrets unavailable: {string.Join(", ", missing)}";
        if (isProduction) throw new InvalidOperationException(message);

        // Development stays runnable — a contributor without production secrets should still be
        // able to start the app — but the warning is loud enough not to be missed.
        logger.LogWarning("{Message}. Continuing because this is not a Production environment.", message);
    }
}
