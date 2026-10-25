using SchoolCalendar.Domain.Enums;

namespace SchoolCalendar.Domain.Entities;

public class CalendarEvent
{
    private CalendarEvent() { }

    internal CalendarEvent(Guid calendarId, string title, string? description, CalendarEventType type, DateOnly startDate, DateOnly endDate, bool isInstructionalDay)
    {
        Id = Guid.NewGuid();
        CalendarId = calendarId;
        CreatedAt = DateTime.UtcNow;
        Update(title, description, type, startDate, endDate, isInstructionalDay);
    }

    public Guid Id { get; private set; }
    public Guid CalendarId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public CalendarEventType Type { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsInstructionalDay { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    internal void Update(string title, string? description, CalendarEventType type, DateOnly startDate, DateOnly endDate, bool isInstructionalDay)
    {
        Title = title;
        Description = description;
        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        IsInstructionalDay = isInstructionalDay;
        UpdatedAt = DateTime.UtcNow;
    }
}
