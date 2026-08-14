using Kurx.Domain.Entities;

namespace Kurx.Domain;

/// <summary>What a brand-new account must supply before it counts as set up, and the order it is asked in.
///
/// <para>The single source of truth for "is this user onboarded". The rule previously existed as two
/// hand-copied expressions — <c>/v1/me</c> tested <c>Name == ""</c> while the registration status endpoint
/// tested <c>IsNullOrWhiteSpace(Name)</c> — so an account whose name was a single space was onboarded
/// according to one endpoint and not the other. Both now call this.</para>
///
/// <para>Password is part of completion, not an optional extra: an account reachable only by SMS code is
/// locked out the moment the number is lost, and the sign-in screen is password-first on every surface.
/// It lives in <c>user_credentials</c> rather than on the user row, which is why callers pass it in.</para></summary>
public static class Onboarding
{
    /// <summary>Youngest permitted account holder. India's DPDP Act treats under-18s as children requiring
    /// verifiable parental consent, which the platform does not implement; 13 is the floor used by the
    /// consent regimes Kurx's own terms inherit from, and events carry their own stricter
    /// <c>MinAge</c> where it matters.</summary>
    public const int MinimumAgeYears = 13;

    /// <summary>Rejects a birth date that is impossible or below <see cref="MinimumAgeYears"/>.
    /// Returns an error code, or null when acceptable.</summary>
    public static string? ValidateDateOfBirth(DateOnly dateOfBirth, DateOnly today)
    {
        if (dateOfBirth > today) return "invalid_date_of_birth";
        if (dateOfBirth < today.AddYears(-120)) return "invalid_date_of_birth";
        return AgeOn(dateOfBirth, today) < MinimumAgeYears ? "under_minimum_age" : null;
    }

    /// <summary>Completed years lived as of <paramref name="today"/> — birthday-aware, so someone whose
    /// birthday falls later this year is still counted as the younger age.</summary>
    public static int AgeOn(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age;
    }

    /// <summary>The steps still outstanding, in the order the client walks them. Empty means fully
    /// onboarded. <paramref name="hasPassword"/> comes from the credential store, and
    /// <paramref name="hasTrustedDevice"/> from the device registry; neither lives on <paramref name="user"/>.</summary>
    public static IReadOnlyList<string> Remaining(User user, bool hasPassword, bool hasTrustedDevice)
    {
        // Blocking steps first: a new account is asked for who it is and a way back in before it is
        // offered the optional extras. Email used to lead, which put a skippable step in front of the
        // two that actually gate the account.
        var remaining = new List<string>();
        if (RequiresProfile(user)) remaining.Add("complete_profile");
        if (!hasPassword) remaining.Add("create_password");
        if (user.Email is null || user.EmailVerifiedAt is null) remaining.Add("verify_email");
        if (!hasTrustedDevice) remaining.Add("enroll_device");
        return remaining;
    }

    /// <summary>When the date-of-birth and password requirements began to apply. Accounts created before
    /// this keep the older name+username rule (D-037).
    ///
    /// <para>A password is required in the <b>account-creation flow</b>. Applying it backwards would take
    /// people who registered under the old contract — and have used the app since — and lock them out of
    /// the app they are already in until they satisfied a rule that did not exist when they signed up.
    /// Onboarding is a gate for new accounts, not a migration tool.</para>
    /// <para>Those accounts are not ignored: the outstanding steps still appear in <see cref="Remaining"/>,
    /// so Security settings can offer them. They are prompted, never trapped.</para></summary>
    /// <para>The value is the timestamp of the <c>AddUserDateOfBirth</c> migration
    /// (<c>20260808193940</c>) — the instant the column began to exist. That makes the boundary a fact
    /// about the schema rather than a date somebody chose: an account older than the column could not
    /// possibly have been asked for a date of birth.</para>
    public static readonly DateTime RequirementsEffectiveFrom =
        new(2026, 8, 8, 19, 39, 40, DateTimeKind.Utc);

    /// <summary>Whether the blocking first-time setup is unfinished — the flag <c>/v1/me</c> publishes as
    /// <c>needs_onboarding</c> and the mobile router redirects on.
    ///
    /// <para>Deliberately narrower than <see cref="Remaining"/>: verifying an email and enrolling a device
    /// are offered during the ceremony but must never trap someone in it, because neither can be completed
    /// by a user who mistyped an address or is on a handset that cannot enrol.</para></summary>
    public static bool IsIncomplete(User user, bool hasPassword)
    {
        // Identity has been required of every account since D-037 and stays required of all of them.
        if (string.IsNullOrWhiteSpace(user.Name) || user.Username is null) return true;
        // Everything below arrived with D-311 and binds only the accounts created under it.
        if (user.CreatedAt < RequirementsEffectiveFrom) return false;
        return user.DateOfBirth is null || !hasPassword;
    }

    /// Listed as outstanding for everyone who is missing something, including the grandfathered accounts
    /// of <see cref="RequirementsEffectiveFrom"/> — being unblocked is not the same as being complete, and
    /// Security settings needs to know the difference to offer the step.
    private static bool RequiresProfile(User user) =>
        string.IsNullOrWhiteSpace(user.Name) || user.Username is null || user.DateOfBirth is null;
}
