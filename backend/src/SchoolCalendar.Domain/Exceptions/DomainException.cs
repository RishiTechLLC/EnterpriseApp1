namespace SchoolCalendar.Domain.Exceptions;

/// <summary>Raised when a business rule is violated. Mapped to HTTP 422 by the API.</summary>
public class DomainException(string message) : Exception(message);
