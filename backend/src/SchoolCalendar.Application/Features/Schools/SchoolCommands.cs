using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Caching;
using SchoolCalendar.Application.Common.Exceptions;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Domain.Entities;

namespace SchoolCalendar.Application.Features.Schools;

// ---------- Create ----------

public sealed record CreateSchoolCommand(string OrganizationId, string Name, string Code, string? Address, string TimeZone)
    : IRequest<Guid>, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Organization, OrganizationId, Permissions.CreateSchool)];
}

public sealed class CreateSchoolValidator : AbstractValidator<CreateSchoolCommand>
{
    public CreateSchoolValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreateSchoolHandler(IAppDbContext db, ICurrentUser user, IPermissionService permify)
    : IRequestHandler<CreateSchoolCommand, Guid>
{
    public async Task<Guid> Handle(CreateSchoolCommand request, CancellationToken cancellationToken)
    {
        var school = new School(request.Name, request.Code, request.Address, request.TimeZone, user.UserId!);
        db.Schools.Add(school);
        await db.SaveChangesAsync(cancellationToken);

        // Wire the new school into the authorization graph: it belongs to the district, and its creator administers it.
        await permify.WriteRelationshipsAsync(
        [
            new(ResourceTypes.School, school.Id.ToString(), Relations.Org, ResourceTypes.Organization, request.OrganizationId),
            new(ResourceTypes.School, school.Id.ToString(), Relations.Admin, ResourceTypes.User, user.UserId!),
        ], cancellationToken);

        return school.Id;
    }
}

// ---------- Update ----------

public sealed record UpdateSchoolCommand(Guid SchoolId, string Name, string Code, string? Address, string TimeZone)
    : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.School, SchoolId.ToString(), Permissions.Edit)];
}

public sealed class UpdateSchoolValidator : AbstractValidator<UpdateSchoolCommand>
{
    public UpdateSchoolValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64);
    }
}

public sealed class UpdateSchoolHandler(IAppDbContext db, ICacheService cache) : IRequestHandler<UpdateSchoolCommand>
{
    public async Task Handle(UpdateSchoolCommand request, CancellationToken cancellationToken)
    {
        var school = await db.Schools.FindAsync([request.SchoolId], cancellationToken)
            ?? throw new NotFoundException(nameof(School), request.SchoolId);
        school.Update(request.Name, request.Code, request.Address, request.TimeZone);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateTagsAsync([CacheTags.School(school.Id)], cancellationToken);
    }
}

// ---------- Delete ----------

public sealed record DeleteSchoolCommand(Guid SchoolId) : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.School, SchoolId.ToString(), Permissions.Manage)];
}

public sealed class DeleteSchoolHandler(IAppDbContext db, IPermissionService permify, ICacheService cache)
    : IRequestHandler<DeleteSchoolCommand>
{
    public async Task Handle(DeleteSchoolCommand request, CancellationToken cancellationToken)
    {
        var school = await db.Schools
            .Include(s => s.Programs).ThenInclude(p => p.Calendars)
            .SingleOrDefaultAsync(s => s.Id == request.SchoolId, cancellationToken)
            ?? throw new NotFoundException(nameof(School), request.SchoolId);

        var programIds = school.Programs.Select(p => p.Id).ToList();
        var calendarIds = school.Programs.SelectMany(p => p.Calendars).Select(c => c.Id).ToList();

        db.Schools.Remove(school);
        await db.SaveChangesAsync(cancellationToken);

        await permify.DeleteEntityDataAsync(ResourceTypes.Calendar, calendarIds.Select(id => id.ToString()), cancellationToken);
        await permify.DeleteEntityDataAsync(ResourceTypes.Program, programIds.Select(id => id.ToString()), cancellationToken);
        await permify.DeleteEntityDataAsync(ResourceTypes.School, [school.Id.ToString()], cancellationToken);

        await cache.InvalidateTagsAsync(
            [CacheTags.School(school.Id), .. programIds.Select(CacheTags.Program), .. calendarIds.Select(CacheTags.Calendar)],
            cancellationToken);
    }
}
