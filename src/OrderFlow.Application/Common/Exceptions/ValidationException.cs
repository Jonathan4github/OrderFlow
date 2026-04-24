using FluentValidation.Results;

namespace OrderFlow.Application.Common.Exceptions;

/// <summary>
/// Thrown by the validation pipeline behavior when one or more
/// <see cref="FluentValidation.IValidator{T}"/> failures are produced for a request.
/// The API layer maps this to HTTP 400 with a problem-details payload.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>Errors grouped by property name.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Creates an empty validation exception.</summary>
    public ValidationException() : base("One or more validation failures occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    /// <summary>Creates a validation exception from a list of FluentValidation failures.</summary>
    public ValidationException(IEnumerable<ValidationFailure> failures) : this()
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}
