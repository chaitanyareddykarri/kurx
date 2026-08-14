using System.Linq;

namespace Kurx.Domain;

/// <summary>V3 §9.1 [B16] — money is never a bare integer. A minor-unit amount (paise for INR) plus an
/// ISO-4217 currency. This is the value type used at the edges (display, gateway) and by future Wave-C
/// money code (Passes/VAR, §9.2/§9.5). Storage keeps the existing <c>*_paise</c> columns as the amount
/// (D-004 preserved) alongside an additive <c>currency</c> column; nothing is rewritten this phase.
///
/// <para>Multi-currency <b>settlement</b> on one event is deliberately out of scope (V3 §9.1). An event
/// settles in exactly one currency, bound from its organization and immutable once money moves.</para></summary>
public readonly record struct Money(long AmountMinor, string Currency)
{
    /// <summary>The platform default until multi-currency settlement is enabled (out of scope, V3 §9.1).</summary>
    public const string DefaultCurrency = "INR";

    public static Money Inr(long amountMinor) => new(amountMinor, DefaultCurrency);

    /// <summary>Lightweight ISO-4217 format check — exactly 3 uppercase ASCII letters (e.g. INR, USD). Not a
    /// full currency registry (V3 keeps multi-currency out of scope); it rejects clearly-malformed codes
    /// wherever a currency becomes writable.</summary>
    public static bool IsValidCurrency(string? code)
        => code is { Length: 3 } && code.All(ch => ch is >= 'A' and <= 'Z');
}
