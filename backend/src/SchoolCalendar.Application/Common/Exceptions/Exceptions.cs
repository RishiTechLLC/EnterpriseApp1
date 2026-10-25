using FluentValidation.Results;

namespace SchoolCalendar.Application.Common.Exceptions;

public class NotFoundException(string entity, object key) : Exception($"{entity} '{key}' was not found.");

public class ForbiddenAccessException(string message = "You do not have permission to perform this action.") : Exception(message);

public class UnauthenticatedException() : Exception("Authentication is required.");

public class ValidationException(IEnumerable<ValidationFailure> failures) : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = failures
        .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
        .ToDictionary(g => g.Key, g => g.Distinct().ToArray());
}
