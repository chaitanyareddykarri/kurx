using Kurx.Infrastructure.Auth;
using Kurx.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Kurx.Tests;

public class SecretValidationTests
{
    // Production now also requires a distinct TICKET_HMAC_SECRET, so the JWT-focused cases below supply a
    // valid one by default and the ticket-specific cases override it.
    private const string StrongTicketSecret = "a-unique-production-ticket-hmac-secret-0123456789";

    private static IConfiguration Config(string? jwtSecret, string? ticketHmacSecret = StrongTicketSecret)
    {
        var values = new Dictionary<string, string?>();
        if (jwtSecret is not null) values["JWT_SECRET"] = jwtSecret;
        if (ticketHmacSecret is not null) values["TICKET_HMAC_SECRET"] = ticketHmacSecret;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private const string StrongJwtSecret = "a-unique-production-secret-that-is-definitely-long-enough-0123456789";

    [Fact]
    public void Missing_secret_throws_regardless_of_environment()
    {
        Assert.Throws<InvalidOperationException>(() => JwtOptions.FromConfiguration(Config(null)));
        Assert.Throws<InvalidOperationException>(() => JwtOptions.FromConfiguration(Config(null), isProduction: true));
    }

    [Fact]
    public void Dev_placeholder_is_accepted_outside_production()
    {
        var options = JwtOptions.FromConfiguration(Config("dev-only-secret-change-me-0123456789abcdef"), isProduction: false);
        Assert.Equal("dev-only-secret-change-me-0123456789abcdef", options.Secret);
    }

    [Fact]
    public void Dev_placeholder_is_rejected_in_production()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtOptions.FromConfiguration(Config("dev-only-secret-change-me-0123456789abcdef"), isProduction: true));
        Assert.Contains("placeholder", ex.Message);
    }

    [Fact]
    public void Short_secret_is_rejected_in_production()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtOptions.FromConfiguration(Config("too-short"), isProduction: true));
        Assert.Contains("32 characters", ex.Message);
    }

    [Fact]
    public void Strong_unique_secret_is_accepted_in_production()
    {
        var options = JwtOptions.FromConfiguration(Config(StrongJwtSecret), isProduction: true);
        Assert.NotEmpty(options.Secret);
        // The two keys must stay distinct: reusing one for both security domains is the bug this guards.
        Assert.NotEqual(options.Secret, options.TicketHmacSecret);
    }

    [Fact]
    public void Missing_ticket_hmac_secret_is_rejected_in_production()
    {
        // Previously this silently fell back to JWT_SECRET, signing gate tickets with the session key.
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtOptions.FromConfiguration(Config(StrongJwtSecret, ticketHmacSecret: null), isProduction: true));
        Assert.Contains("TICKET_HMAC_SECRET", ex.Message);
    }

    [Fact]
    public void Dev_placeholder_ticket_hmac_secret_is_rejected_in_production()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtOptions.FromConfiguration(Config(StrongJwtSecret, "dev-only-ticket-hmac-change-me"), isProduction: true));
        Assert.Contains("TICKET_HMAC_SECRET", ex.Message);
    }

    [Fact]
    public void Missing_ticket_hmac_secret_falls_back_to_the_jwt_secret_outside_production()
    {
        // Zero-config dev stays runnable for a contributor who only set JWT_SECRET.
        var options = JwtOptions.FromConfiguration(Config(StrongJwtSecret, ticketHmacSecret: null), isProduction: false);
        Assert.Equal(options.Secret, options.TicketHmacSecret);
    }

    /// <summary>OTP_PEPPER was presence-checked but never strength-checked, so the committed placeholder
    /// booted a Production host clean. It is the one secret whose misconfiguration nothing downstream ever
    /// surfaces: OTP hashes computed under a guessable pepper behave exactly like correct ones (D-115).</summary>
    [Fact]
    public void Dev_placeholder_otp_pepper_is_rejected_in_production()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretValidation.RequireStrongProductionSecret("OTP_PEPPER", "dev-only-otp-pepper-change-me"));
        Assert.Contains("development placeholder", ex.Message);
    }

    [Fact]
    public void Strong_unique_otp_pepper_is_accepted_in_production()
    {
        SecretValidation.RequireStrongProductionSecret("OTP_PEPPER", "a-unique-production-otp-pepper-0123456789");
    }

    [Fact]
    public void Connection_string_with_dev_password_is_rejected_in_production()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SecretValidation.RequireProductionConnectionString(
            "ConnectionStrings__Default", "Host=prod-db;Port=5432;Database=kurx;Username=kurx;Password=kurx"));
        Assert.Contains("development password", ex.Message);
    }

    [Fact]
    public void Connection_string_with_real_password_is_accepted_in_production()
    {
        SecretValidation.RequireProductionConnectionString(
            "ConnectionStrings__Default", "Host=prod-db;Port=5432;Database=kurx;Username=kurx;Password=s3cr3t-real-value");
    }
}
