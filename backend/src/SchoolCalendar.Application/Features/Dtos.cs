using SchoolCalendar.Domain.Entities;
using SchoolCalendar.Domain.Enums;

namespace SchoolCalendar.Application.Features;

public sealed record SchoolDto(Guid Id, string Name, string Code, string? Address, string TimeZone, int ProgramCount, DateTime CreatedAt);

public sealed record ProgramDto(Guid Id, Guid SchoolId, string Name, string Code, string? Description, string? GradeLevels, int CalendarCount);

public sealed record CalendarSummaryDto(
    Guid Id, Guid ProgramId, string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate,
    CalendarStatus Status, int EventCount);

public sealed record CalendarDetailDto(
    Guid Id, Guid ProgramId, Guid SchoolId, string ProgramName, string SchoolName, string Name, string AcademicYear,
    DateOnly StartDate, DateOnly EndDate, string? Description, CalendarStatus Status, DateTime? PublishedAt,
    int InstructionalDays, IReadOnlyList<CalendarEventDto> Events);

public sealed record CalendarEventDto(
    Guid Id, Guid CalendarId, string Title, string? Description, CalendarEventType Type,
    DateOnly StartDate, DateOnly EndDate, bool IsInstructionalDay)
{
    public static CalendarEventDto From(CalendarEvent e) =>
        new(e.Id, e.CalendarId, e.Title, e.Description, e.Type, e.StartDate, e.EndDate, e.IsInstructionalDay);
}

public sealed record MemberDto(string UserId, string Role);
