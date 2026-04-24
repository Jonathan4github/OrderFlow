namespace OrderFlow.Domain.ValueObjects;

/// <summary>
/// Monetary value with an explicit ISO-4217 currency code. Immutable.
/// </summary>
/// <remarks>
/// Arithmetic between two <see cref="Money"/> values is only valid when both sides share
/// the same currency; attempting to combine different currencies throws an
/// <see cref="InvalidOperationException"/>. Negative amounts are disallowed because the
/// domain does not model debits or refunds yet.
/// </remarks>
public sealed record Money
{
    /// <summary>Default ISO-4217 code used when no currency is supplied.</summary>
    public const string DefaultCurrency = "USD";

    /// <summary>Amount as a decimal.</summary>
    public decimal Amount { get; }

    /// <summary>ISO-4217 currency code (uppercase, 3 letters).</summary>
    public string Currency { get; }

    /// <summary>Creates a new monetary value.</summary>
    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO-4217 code.", nameof(currency));
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency.ToUpperInvariant();
    }

    /// <summary>Zero amount in the supplied currency.</summary>
    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    /// <summary>Adds two monetary values of the same currency.</summary>
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>Subtracts <paramref name="right"/> from <paramref name="left"/> (both same currency).</summary>
    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>Multiplies a monetary value by a scalar quantity.</summary>
    public static Money operator *(Money left, int multiplier)
    {
        if (multiplier < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "Multiplier must be non-negative.");
        }

        return new Money(left.Amount * multiplier, left.Currency);
    }

    /// <inheritdoc cref="op_Multiply(Money, int)" />
    public static Money operator *(int multiplier, Money right) => right * multiplier;

    /// <inheritdoc />
    public override string ToString() => $"{Amount:0.00} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine Money values of different currencies: {left.Currency} vs {right.Currency}.");
        }
    }
}
