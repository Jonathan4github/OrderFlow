namespace OrderFlow.Domain.Exceptions;

/// <summary>
/// Base exception for every domain-level invariant violation.
/// Application and API layers map derived types to appropriate HTTP status codes.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>Creates a new domain exception.</summary>
    protected DomainException(string message) : base(message)
    {
    }

    /// <summary>Creates a new domain exception with an inner cause.</summary>
    protected DomainException(string message, Exception inner) : base(message, inner)
    {
    }
}
