using SchoolCalendar.Domain.Enums;
using SchoolCalendar.Domain.Exceptions;

namespace SchoolCalendar.Domain.Entities;

/// <summary>
/// A calendar for one program in one academic year (e.g. "2026-2027 High School Calendar").
/// </summary>
public class AcademicCalendar
{
    private readonly List<CalendarEvent> _events = [];

    private AcademicCalendar() { }

    public AcademicCalendar(Guid programId, string name, string academicYear, DateOnly startDate, DateOnly endDate, string? description)
    {
        EnsureValidRange(startDate, endDate);
        Id = Guid.NewGuid();
        ProgramId = programId;
        Name = name;
        AcademicYear = academicYear;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
        Status = CalendarStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProgramId { get; private set; }
    public AcademicProgram Program { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string AcademicYear { get; private set; } = default!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string? Description { get; private set; }
    public CalendarStatus Status { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<CalendarEvent> Events => _events;

    public void Update(string name, string academicYear, DateOnly startDate, DateOnly endDate, string? description)
    {
        EnsureValidRange(startDate, endDate);
        if (_events.Any(e => e.StartDate < startDate || e.EndDate > endDate))
            throw new DomainException("Existing events fall outside the new calendar date range.");

        Name = name;
        AcademicYear = academicYear;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public CalendarEvent AddEvent(string title, string? description, CalendarEventType type, DateOnly startDate, DateOnly endDate, bool isInstructionalDay)
    {
        EnsureEventInRange(startDate, endDate);
        var calendarEvent = new CalendarEvent(Id, title, description, type, startDate, endDate, isInstructionalDay);
        _events.Add(calendarEvent);
        UpdatedAt = DateTime.UtcNow;
        return calendarEvent;
    }

    public void UpdateEvent(Guid eventId, string title, string? description, CalendarEventType type, DateOnly startDate, DateOnly endDate, bool isInstructionalDay)
    {
        var calendarEvent = _events.SingleOrDefault(e => e.Id == eventId)
            ?? throw new DomainException($"Event {eventId} does not belong to calendar {Id}.");
        EnsureEventInRange(startDate, endDate);
        calendarEvent.Update(title, description, type, startDate, endDate, isInstructionalDay);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveEvent(Guid eventId)
    {
        var calendarEvent = _events.SingleOrDefault(e => e.Id == eventId)
            ?? throw new DomainException($"Event {eventId} does not belong to calendar {Id}.");
        _events.Remove(calendarEvent);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        if (Status == CalendarStatus.Published)
            throw new DomainException("Calendar is already published.");
        Status = CalendarStatus.Published;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        if (Status != CalendarStatus.Published)
            throw new DomainException("Calendar is not published.");
        Status = CalendarStatus.Draft;
        PublishedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Number of instructional days: weekdays in range that aren't covered by a non-instructional event.</summary>
    public int CountInstructionalDays()
    {
        var count = 0;
        for (var day = StartDate; day <= EndDate; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var blocked = _events.Any(e => !e.IsInstructionalDay && e.StartDate <= day && e.EndDate >= day);
            if (!blocked) count++;
        }
        return count;
    }

    private void EnsureEventInRange(DateOnly startDate, DateOnly endDate)
    {
        EnsureValidRange(startDate, endDate);
        if (startDate < StartDate || endDate > EndDate)
            throw new DomainException($"Event dates must fall within the calendar range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}.");
    }

    private static void EnsureValidRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            throw new DomainException("End date must be on or after start date.");
    }
}
