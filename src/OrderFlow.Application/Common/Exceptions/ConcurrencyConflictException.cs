namespace OrderFlow.Application.Common.Exceptions;

/// <summary>
/// Raised when a write is rejected because another transaction modified the
/// same row between our read and our commit. The API maps this to HTTP 409
/// with a message encouraging the client to retry.
/// </summary>
/// <remarks>
/// Under normal operation the pessimistic <c>SELECT ... FOR UPDATE SKIP LOCKED</c>
/// acquired by the reservation flow makes this exceptional. It is the last-line
/// defence for writers that bypass the lock (ad-hoc scripts, background jobs).
/// </remarks>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Creates a new <see cref="ConcurrencyConflictException"/>.</summary>
    public ConcurrencyConflictException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}