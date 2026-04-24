namespace OrderFlow.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on an order whose current
/// status does not permit the transition (for example, confirming
/// payment on an already-cancelled order).
/// </summary>
public sealed class InvalidOrderStateException : DomainException
{
    /// <summary>Creates a new <see cref="InvalidOrderStateException"/>.</summary>
    public InvalidOrderStateException(string message) : base(message)
    {
    }
}
