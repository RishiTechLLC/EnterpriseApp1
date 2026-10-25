using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Caching;
using SchoolCalendar.Application.Common.Exceptions;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Domain.Enums;

namespace SchoolCalendar.Application.Features.Calendars;

/// <summary>
/// Calendars of a program. The full list is cached per program; drafts are then filtered out
/// for callers who can only view (not edit) the program.
/// </summary>
public sealed record GetCalendarsByProgramQuery(Guid ProgramId) : IRequest<IReadOnlyList<CalendarSummaryDto>>, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Program, ProgramId.ToString(), Permissions.View)];
}

public sealed class GetCalendarsByProgramHandler(IAppDbContext db, ICacheService cache, ICurrentUser user, IPermissionService permify)
    : IRequestHandler<GetCalendarsByProgramQuery, IReadOnlyList<CalendarSummaryDto>>
{
    public async Task<IReadOnlyList<CalendarSummaryDto>> Handle(GetCalendarsByProgramQuery request, CancellationToken cancellationToken)
    {
        var key = $"program:{request.ProgramId}:calendars";
        IReadOnlyCollection<string> tags = [CacheTags.Program(request.ProgramId)];

        var all = await cache.GetAsync<List<CalendarSummaryDto>>(key, tags, cancellationToken);
        if (all is null)
        {
            all = await db.Calendars.AsNoTracking()
                .Where(c => c.ProgramId == request.ProgramId)
                .OrderByDescending(c => c.StartDate)
                .Select(c => new CalendarSummaryDto(c.Id, c.ProgramId, c.Name, c.AcademicYear, c.StartDate, c.EndDate, c.Status, c.Events.Count))
                .ToListAsync(cancellationToken);
            await cache.SetAsync(key, all, tags, cancellationToken: cancellationToken);
        }

        var canEdit = await permify.CheckAsync(
            new(ResourceTypes.Program, request.ProgramId.ToString(), Permissions.Edit), user.UserId!, cancellationToken);
        return canEdit ? all : all.Where(c => c.Status == CalendarStatus.Published).ToList();
    }
}

public sealed record GetCalendarByIdQuery(Guid CalendarId) : IRequest<CalendarDetailDto>, IAuthorizedRequest, ICacheableQuery
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.View)];

    public string CacheKey => $"calendar:{CalendarId}:detail";
    public IReadOnlyCollection<string> CacheTags => [Common.Caching.CacheTags.Calendar(CalendarId)];
}

public sealed class GetCalendarByIdHandler(IAppDbContext db) : IRequestHandler<GetCalendarByIdQuery, CalendarDetailDto>
{
    public async Task<CalendarDetailDto> Handle(GetCalendarByIdQuery request, CancellationToken cancellationToken)
    {
        var c = await db.Calendars.AsNoTracking()
            .Include(x => x.Events)
            .Include(x => x.Program).ThenInclude(p => p.School)
            .SingleOrDefaultAsync(x => x.Id == request.CalendarId, cancellationToken)
            ?? throw new NotFoundException("Calendar", request.CalendarId);

        return new CalendarDetailDto(
            c.Id, c.ProgramId, c.Program.SchoolId, c.Program.Name, c.Program.School.Name, c.Name, c.AcademicYear,
            c.StartDate, c.EndDate, c.Description, c.Status, c.PublishedAt, c.CountInstructionalDays(),
            c.Events.OrderBy(e => e.StartDate).ThenBy(e => e.Title).Select(CalendarEventDto.From).ToList());
    }
}

/// <summary>Exports a calendar in iCalendar (RFC 5545) format so families can subscribe from their phones.</summary>
public sealed record ExportCalendarIcsQuery(Guid CalendarId) : IRequest<string>, IAuthorizedRequest, ICacheableQuery
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.View)];

    public string CacheKey => $"calendar:{CalendarId}:ics";
    public IReadOnlyCollection<string> CacheTags => [Common.Caching.CacheTags.Calendar(CalendarId)];
}

public sealed class ExportCalendarIcsHandler(ISender sender) : IRequestHandler<ExportCalendarIcsQuery, string>
{
    public async Task<string> Handle(ExportCalendarIcsQuery request, CancellationToken cancellationToken)
    {
        var calendar = await sender.Send(new GetCalendarByIdQuery(request.CalendarId), cancellationToken);
        var sb = new StringBuilder()
            .Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//SchoolCalendar//EN\r\nCALSCALE:GREGORIAN\r\n")
            .Append($"X-WR-CALNAME:{Escape($"{calendar.SchoolName} - {calendar.Name}")}\r\n");
        foreach (var e in calendar.Events)
        {
            sb.Append("BEGIN:VEVENT\r\n")
              .Append($"UID:{e.Id}@schoolcalendar\r\n")
              .Append($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}\r\n")
              .Append($"DTSTART;VALUE=DATE:{e.StartDate:yyyyMMdd}\r\n")
              .Append($"DTEND;VALUE=DATE:{e.EndDate.AddDays(1):yyyyMMdd}\r\n") // DTEND is exclusive for all-day events
              .Append($"SUMMARY:{Escape(e.Title)}\r\n")
              .Append($"CATEGORIES:{e.Type}\r\n");
            if (!string.IsNullOrWhiteSpace(e.Description)) sb.Append($"DESCRIPTION:{Escape(e.Description)}\r\n");
            sb.Append("END:VEVENT\r\n");
        }
        return sb.Append("END:VCALENDAR\r\n").ToString();
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");
}
