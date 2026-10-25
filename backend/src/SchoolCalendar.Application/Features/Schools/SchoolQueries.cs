using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Caching;
using SchoolCalendar.Application.Common.Exceptions;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Domain.Entities;

namespace SchoolCalendar.Application.Features.Schools;

/// <summary>Schools the caller can view. Permify's lookup-entity decides which ids are visible.</summary>
public sealed record GetMySchoolsQuery : IRequest<IReadOnlyList<SchoolDto>>, IAuthorizedByLookup;

public sealed class GetMySchoolsHandler(IAppDbContext db, ICurrentUser user, IPermissionService permify)
    : IRequestHandler<GetMySchoolsQuery, IReadOnlyList<SchoolDto>>
{
    public async Task<IReadOnlyList<SchoolDto>> Handle(GetMySchoolsQuery request, CancellationToken cancellationToken)
    {
        var ids = (await permify.LookupEntityIdsAsync(ResourceTypes.School, Permissions.View, user.UserId!, cancellationToken))
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        return await db.Schools.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .OrderBy(s => s.Name)
            .Select(s => new SchoolDto(s.Id, s.Name, s.Code, s.Address, s.TimeZone, s.Programs.Count, s.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetSchoolByIdQuery(Guid SchoolId) : IRequest<SchoolDto>, IAuthorizedRequest, ICacheableQuery
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.School, SchoolId.ToString(), Permissions.View)];

    public string CacheKey => $"school:{SchoolId}:detail";
    public IReadOnlyCollection<string> CacheTags => [Common.Caching.CacheTags.School(SchoolId)];
}

public sealed class GetSchoolByIdHandler(IAppDbContext db) : IRequestHandler<GetSchoolByIdQuery, SchoolDto>
{
    public async Task<SchoolDto> Handle(GetSchoolByIdQuery request, CancellationToken cancellationToken) =>
        await db.Schools.AsNoTracking()
            .Where(s => s.Id == request.SchoolId)
            .Select(s => new SchoolDto(s.Id, s.Name, s.Code, s.Address, s.TimeZone, s.Programs.Count, s.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(School), request.SchoolId);
}
