using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Caching;
using SchoolCalendar.Application.Common.Exceptions;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Domain.Entities;

namespace SchoolCalendar.Application.Features.Calendars;

internal static class CalendarRules
{
    public static void Apply<T>(AbstractValidator<T> v, Func<T, string> name, Func<T, string> year,
        Func<T, DateOnly> start, Func<T, DateOnly> end, Func<T, string?> description)
    {
        v.RuleFor(x => name(x)).NotEmpty().MaximumLength(200).OverridePropertyName("Name");
        v.RuleFor(x => year(x)).NotEmpty().Matches(@"^\d{4}-\d{4}$").WithMessage("Academic year must look like 2026-2027.")
            .OverridePropertyName("AcademicYear");
        v.RuleFor(x => end(x)).GreaterThanOrEqualTo(x => start(x)).WithMessage("End date must be on or after start date.")
            .OverridePropertyName("EndDate");
        v.RuleFor(x => end(x)).Must((x, e) => e.DayNumber - start(x).DayNumber <= 731)
            .WithMessage("A calendar cannot span more than two years.").OverridePropertyName("EndDate");
        v.RuleFor(x => description(x)).MaximumLength(2000).OverridePropertyName("Description");
    }

    public static async Task<AcademicCalendar> LoadAsync(IAppDbContext db, Guid calendarId, CancellationToken ct, bool withEvents = false)
    {
        IQueryable<AcademicCalendar> query = db.Calendars;
        if (withEvents) query = query.Include(c => c.Events);
        return await query.SingleOrDefaultAsync(c => c.Id == calendarId, ct)
            ?? throw new NotFoundException("Calendar", calendarId);
    }

    public static Task InvalidateAsync(ICacheService cache, AcademicCalendar calendar, CancellationToken ct) =>
        cache.InvalidateTagsAsync([CacheTags.Calendar(calendar.Id), CacheTags.Program(calendar.ProgramId)], ct);
}

// ---------- Create ----------

public sealed record CreateCalendarCommand(Guid ProgramId, string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, string? Description)
    : IRequest<Guid>, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Program, ProgramId.ToString(), Permissions.Edit)];
}

public sealed class CreateCalendarValidator : AbstractValidator<CreateCalendarCommand>
{
    public CreateCalendarValidator() =>
        CalendarRules.Apply(this, x => x.Name, x => x.AcademicYear, x => x.StartDate, x => x.EndDate, x => x.Description);
}

public sealed class CreateCalendarHandler(IAppDbContext db, IPermissionService permify, ICacheService cache)
    : IRequestHandler<CreateCalendarCommand, Guid>
{
    public async Task<Guid> Handle(CreateCalendarCommand request, CancellationToken cancellationToken)
    {
        var program = await db.Programs.FindAsync([request.ProgramId], cancellationToken)
            ?? throw new NotFoundException("Program", request.ProgramId);

        var calendar = new AcademicCalendar(program.Id, request.Name, request.AcademicYear, request.StartDate, request.EndDate, request.Description);
        db.Calendars.Add(calendar);
        await db.SaveChangesAsync(cancellationToken);

        await permify.WriteRelationshipsAsync(
            [new(ResourceTypes.Calendar, calendar.Id.ToString(), Relations.Program, ResourceTypes.Program, program.Id.ToString())],
            cancellationToken);
        await permify.WriteBooleanAttributeAsync(ResourceTypes.Calendar, calendar.Id.ToString(), Attributes.IsPublished, false, cancellationToken);

        await cache.InvalidateTagsAsync([CacheTags.Program(program.Id), CacheTags.School(program.SchoolId)], cancellationToken);
        return calendar.Id;
    }
}

// ---------- Update ----------

public sealed record UpdateCalendarCommand(Guid CalendarId, string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, string? Description)
    : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.Edit)];
}

public sealed class UpdateCalendarValidator : AbstractValidator<UpdateCalendarCommand>
{
    public UpdateCalendarValidator() =>
        CalendarRules.Apply(this, x => x.Name, x => x.AcademicYear, x => x.StartDate, x => x.EndDate, x => x.Description);
}

public sealed class UpdateCalendarHandler(IAppDbContext db, ICacheService cache) : IRequestHandler<UpdateCalendarCommand>
{
    public async Task Handle(UpdateCalendarCommand request, CancellationToken cancellationToken)
    {
        var calendar = await CalendarRules.LoadAsync(db, request.CalendarId, cancellationToken, withEvents: true);
        calendar.Update(request.Name, request.AcademicYear, request.StartDate, request.EndDate, request.Description);
        await db.SaveChangesAsync(cancellationToken);
        await CalendarRules.InvalidateAsync(cache, calendar, cancellationToken);
    }
}

// ---------- Publish / Unpublish ----------

public sealed record SetCalendarPublishedCommand(Guid CalendarId, bool Published) : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.Publish)];
}

public sealed class SetCalendarPublishedHandler(IAppDbContext db, IPermissionService permify, ICacheService cache)
    : IRequestHandler<SetCalendarPublishedCommand>
{
    public async Task Handle(SetCalendarPublishedCommand request, CancellationToken cancellationToken)
    {
        var calendar = await CalendarRules.LoadAsync(db, request.CalendarId, cancellationToken);
        if (request.Published) calendar.Publish(); else calendar.Unpublish();
        await db.SaveChangesAsync(cancellationToken);

        // Visibility of drafts is enforced by Permify (ABAC attribute), so keep it in sync.
        await permify.WriteBooleanAttributeAsync(ResourceTypes.Calendar, calendar.Id.ToString(), Attributes.IsPublished, request.Published, cancellationToken);
        await CalendarRules.InvalidateAsync(cache, calendar, cancellationToken);
    }
}

// ---------- Delete ----------

public sealed record DeleteCalendarCommand(Guid CalendarId) : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.Delete)];
}

public sealed class DeleteCalendarHandler(IAppDbContext db, IPermissionService permify, ICacheService cache)
    : IRequestHandler<DeleteCalendarCommand>
{
    public async Task Handle(DeleteCalendarCommand request, CancellationToken cancellationToken)
    {
        var calendar = await CalendarRules.LoadAsync(db, request.CalendarId, cancellationToken);
        var schoolId = await db.Programs.Where(p => p.Id == calendar.ProgramId).Select(p => p.SchoolId).SingleAsync(cancellationToken);

        db.Calendars.Remove(calendar);
        await db.SaveChangesAsync(cancellationToken);

        await permify.DeleteEntityDataAsync(ResourceTypes.Calendar, [calendar.Id.ToString()], cancellationToken);
        await cache.InvalidateTagsAsync(
            [CacheTags.Calendar(calendar.Id), CacheTags.Program(calendar.ProgramId), CacheTags.School(schoolId)], cancellationToken);
    }
}
