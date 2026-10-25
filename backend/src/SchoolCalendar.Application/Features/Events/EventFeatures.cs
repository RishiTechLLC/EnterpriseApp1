using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Caching;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Application.Features.Calendars;
using SchoolCalendar.Domain.Enums;

namespace SchoolCalendar.Application.Features.Events;

// Events are always addressed through their calendar (/calendars/{calendarId}/events/{eventId}),
// so the Permify check on the calendar covers the event and the aggregate rejects foreign event ids.

public sealed record AddEventCommand(Guid CalendarId, string Title, string? Description, CalendarEventType Type, DateOnly StartDate, DateOnly EndDate, bool IsInstructionalDay)
    : IRequest<CalendarEventDto>, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.Edit)];
}

public sealed record UpdateEventCommand(Guid CalendarId, Guid EventId, string Title, string? Description, CalendarEventType Type, DateOnly StartDate, DateOnly EndDate, bool IsInstructionalDay)
    : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.Edit)];
}

public sealed record DeleteEventCommand(Guid CalendarId, Guid EventId) : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.Edit)];
}

public sealed class AddEventValidator : AbstractValidator<AddEventCommand>
{
    public AddEventValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date must be on or after start date.");
    }
}

public sealed class UpdateEventValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date must be on or after start date.");
    }
}

public sealed class EventCommandHandlers(IAppDbContext db, ICacheService cache) :
    IRequestHandler<AddEventCommand, CalendarEventDto>,
    IRequestHandler<UpdateEventCommand>,
    IRequestHandler<DeleteEventCommand>
{
    public async Task<CalendarEventDto> Handle(AddEventCommand r, CancellationToken ct)
    {
        var calendar = await CalendarRules.LoadAsync(db, r.CalendarId, ct, withEvents: true);
        var created = calendar.AddEvent(r.Title, r.Description, r.Type, r.StartDate, r.EndDate, r.IsInstructionalDay);
        db.CalendarEvents.Add(created);
        await db.SaveChangesAsync(ct);
        await CalendarRules.InvalidateAsync(cache, calendar, ct);
        return CalendarEventDto.From(created);
    }

    public async Task Handle(UpdateEventCommand r, CancellationToken ct)
    {
        var calendar = await CalendarRules.LoadAsync(db, r.CalendarId, ct, withEvents: true);
        calendar.UpdateEvent(r.EventId, r.Title, r.Description, r.Type, r.StartDate, r.EndDate, r.IsInstructionalDay);
        await db.SaveChangesAsync(ct);
        await CalendarRules.InvalidateAsync(cache, calendar, ct);
    }

    public async Task Handle(DeleteEventCommand r, CancellationToken ct)
    {
        var calendar = await CalendarRules.LoadAsync(db, r.CalendarId, ct, withEvents: true);
        calendar.RemoveEvent(r.EventId);
        await db.SaveChangesAsync(ct);
        await CalendarRules.InvalidateAsync(cache, calendar, ct);
    }
}

public sealed record GetCalendarEventsQuery(Guid CalendarId, DateOnly? From, DateOnly? To, CalendarEventType? Type)
    : IRequest<IReadOnlyList<CalendarEventDto>>, IAuthorizedRequest, ICacheableQuery
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.View)];

    public string CacheKey => $"calendar:{CalendarId}:events:{From:yyyyMMdd}:{To:yyyyMMdd}:{Type}";
    public IReadOnlyCollection<string> CacheTags => [Common.Caching.CacheTags.Calendar(CalendarId)];
}

public sealed class GetCalendarEventsHandler(IAppDbContext db) : IRequestHandler<GetCalendarEventsQuery, IReadOnlyList<CalendarEventDto>>
{
    public async Task<IReadOnlyList<CalendarEventDto>> Handle(GetCalendarEventsQuery r, CancellationToken ct)
    {
        var query = db.CalendarEvents.AsNoTracking().Where(e => e.CalendarId == r.CalendarId);
        if (r.From is { } from) query = query.Where(e => e.EndDate >= from);
        if (r.To is { } to) query = query.Where(e => e.StartDate <= to);
        if (r.Type is { } type) query = query.Where(e => e.Type == type);

        var events = await query.OrderBy(e => e.StartDate).ThenBy(e => e.Title).ToListAsync(ct);
        return events.Select(CalendarEventDto.From).ToList();
    }
}
