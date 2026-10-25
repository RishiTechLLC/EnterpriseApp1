using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Caching;
using SchoolCalendar.Application.Common.Exceptions;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Domain.Entities;

namespace SchoolCalendar.Application.Features.Programs;

// ---------- Commands ----------

public sealed record CreateProgramCommand(Guid SchoolId, string Name, string Code, string? Description, string? GradeLevels)
    : IRequest<Guid>, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.School, SchoolId.ToString(), Permissions.Manage)];
}

public sealed class CreateProgramValidator : AbstractValidator<CreateProgramCommand>
{
    public CreateProgramValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.GradeLevels).MaximumLength(100);
    }
}

public sealed class CreateProgramHandler(IAppDbContext db, IPermissionService permify, ICacheService cache)
    : IRequestHandler<CreateProgramCommand, Guid>
{
    public async Task<Guid> Handle(CreateProgramCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Schools.AnyAsync(s => s.Id == request.SchoolId, cancellationToken))
            throw new NotFoundException(nameof(School), request.SchoolId);
        if (await db.Programs.AnyAsync(p => p.SchoolId == request.SchoolId && p.Code == request.Code, cancellationToken))
            throw new Common.Exceptions.ValidationException([new(nameof(request.Code), $"Program code '{request.Code}' already exists in this school.")]);

        var program = new AcademicProgram(request.SchoolId, request.Name, request.Code, request.Description, request.GradeLevels);
        db.Programs.Add(program);
        await db.SaveChangesAsync(cancellationToken);

        await permify.WriteRelationshipsAsync(
            [new(ResourceTypes.Program, program.Id.ToString(), Relations.School, ResourceTypes.School, request.SchoolId.ToString())],
            cancellationToken);
        await cache.InvalidateTagsAsync([CacheTags.School(request.SchoolId)], cancellationToken);
        return program.Id;
    }
}

public sealed record UpdateProgramCommand(Guid ProgramId, string Name, string Code, string? Description, string? GradeLevels)
    : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Program, ProgramId.ToString(), Permissions.Manage)];
}

public sealed class UpdateProgramValidator : AbstractValidator<UpdateProgramCommand>
{
    public UpdateProgramValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.GradeLevels).MaximumLength(100);
    }
}

public sealed class UpdateProgramHandler(IAppDbContext db, ICacheService cache) : IRequestHandler<UpdateProgramCommand>
{
    public async Task Handle(UpdateProgramCommand request, CancellationToken cancellationToken)
    {
        var program = await db.Programs.FindAsync([request.ProgramId], cancellationToken)
            ?? throw new NotFoundException("Program", request.ProgramId);
        program.Update(request.Name, request.Code, request.Description, request.GradeLevels);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateTagsAsync([CacheTags.Program(program.Id), CacheTags.School(program.SchoolId)], cancellationToken);
    }
}

public sealed record DeleteProgramCommand(Guid ProgramId) : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Program, ProgramId.ToString(), Permissions.Manage)];
}

public sealed class DeleteProgramHandler(IAppDbContext db, IPermissionService permify, ICacheService cache)
    : IRequestHandler<DeleteProgramCommand>
{
    public async Task Handle(DeleteProgramCommand request, CancellationToken cancellationToken)
    {
        var program = await db.Programs.Include(p => p.Calendars)
            .SingleOrDefaultAsync(p => p.Id == request.ProgramId, cancellationToken)
            ?? throw new NotFoundException("Program", request.ProgramId);
        var calendarIds = program.Calendars.Select(c => c.Id).ToList();

        db.Programs.Remove(program);
        await db.SaveChangesAsync(cancellationToken);

        await permify.DeleteEntityDataAsync(ResourceTypes.Calendar, calendarIds.Select(id => id.ToString()), cancellationToken);
        await permify.DeleteEntityDataAsync(ResourceTypes.Program, [program.Id.ToString()], cancellationToken);
        await cache.InvalidateTagsAsync(
            [CacheTags.Program(program.Id), CacheTags.School(program.SchoolId), .. calendarIds.Select(CacheTags.Calendar)],
            cancellationToken);
    }
}

// ---------- Queries ----------

public sealed record GetProgramsBySchoolQuery(Guid SchoolId) : IRequest<IReadOnlyList<ProgramDto>>, IAuthorizedRequest, ICacheableQuery
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.School, SchoolId.ToString(), Permissions.View)];

    public string CacheKey => $"school:{SchoolId}:programs";
    public IReadOnlyCollection<string> CacheTags => [Common.Caching.CacheTags.School(SchoolId)];
}

public sealed class GetProgramsBySchoolHandler(IAppDbContext db) : IRequestHandler<GetProgramsBySchoolQuery, IReadOnlyList<ProgramDto>>
{
    public async Task<IReadOnlyList<ProgramDto>> Handle(GetProgramsBySchoolQuery request, CancellationToken cancellationToken) =>
        await db.Programs.AsNoTracking()
            .Where(p => p.SchoolId == request.SchoolId)
            .OrderBy(p => p.Name)
            .Select(p => new ProgramDto(p.Id, p.SchoolId, p.Name, p.Code, p.Description, p.GradeLevels, p.Calendars.Count))
            .ToListAsync(cancellationToken);
}

public sealed record GetProgramByIdQuery(Guid ProgramId) : IRequest<ProgramDto>, IAuthorizedRequest, ICacheableQuery
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Program, ProgramId.ToString(), Permissions.View)];

    public string CacheKey => $"program:{ProgramId}:detail";
    public IReadOnlyCollection<string> CacheTags => [Common.Caching.CacheTags.Program(ProgramId)];
}

public sealed class GetProgramByIdHandler(IAppDbContext db) : IRequestHandler<GetProgramByIdQuery, ProgramDto>
{
    public async Task<ProgramDto> Handle(GetProgramByIdQuery request, CancellationToken cancellationToken) =>
        await db.Programs.AsNoTracking()
            .Where(p => p.Id == request.ProgramId)
            .Select(p => new ProgramDto(p.Id, p.SchoolId, p.Name, p.Code, p.Description, p.GradeLevels, p.Calendars.Count))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Program", request.ProgramId);
}
