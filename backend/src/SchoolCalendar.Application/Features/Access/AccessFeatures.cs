using FluentValidation;
using MediatR;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Interfaces;

namespace SchoolCalendar.Application.Features.Access;

internal static class AccessRules
{
    public static readonly string[] ResourcesWithPermissions = [ResourceTypes.School, ResourceTypes.Program, ResourceTypes.Calendar];

    public static readonly IReadOnlyDictionary<string, string[]> PermissionsByResource = new Dictionary<string, string[]>
    {
        [ResourceTypes.School] = [Permissions.View, Permissions.Edit, Permissions.Manage],
        [ResourceTypes.Program] = [Permissions.View, Permissions.Edit, Permissions.Manage],
        [ResourceTypes.Calendar] = [Permissions.View, Permissions.Edit, Permissions.Publish, Permissions.Delete],
    };
}

/// <summary>What the caller may do on a resource, so the UI can show or hide actions. The API still enforces every call.</summary>
public sealed record GetMyPermissionsQuery(string ResourceType, Guid ResourceId) : IRequest<IReadOnlyDictionary<string, bool>>, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceType, ResourceId.ToString(), Permissions.View)];
}

public sealed class GetMyPermissionsValidator : AbstractValidator<GetMyPermissionsQuery>
{
    public GetMyPermissionsValidator() =>
        RuleFor(x => x.ResourceType).Must(t => AccessRules.ResourcesWithPermissions.Contains(t)).WithMessage("Unknown resource type.");
}

public sealed class GetMyPermissionsHandler(ICurrentUser user, IPermissionService permify)
    : IRequestHandler<GetMyPermissionsQuery, IReadOnlyDictionary<string, bool>>
{
    public async Task<IReadOnlyDictionary<string, bool>> Handle(GetMyPermissionsQuery r, CancellationToken ct)
    {
        var names = AccessRules.PermissionsByResource[r.ResourceType];
        var results = await Task.WhenAll(names.Select(p =>
            permify.CheckAsync(new(r.ResourceType, r.ResourceId.ToString(), p), user.UserId!, ct)));
        return names.Zip(results).ToDictionary(x => x.First, x => x.Second);
    }
}

/// <summary>Can the caller create schools in the organization?</summary>
public sealed record CanCreateSchoolQuery(string OrganizationId) : IRequest<bool>, IAuthorizedByLookup;

public sealed class CanCreateSchoolHandler(ICurrentUser user, IPermissionService permify) : IRequestHandler<CanCreateSchoolQuery, bool>
{
    public Task<bool> Handle(CanCreateSchoolQuery r, CancellationToken ct) =>
        permify.CheckAsync(new(ResourceTypes.Organization, r.OrganizationId, Permissions.CreateSchool), user.UserId!, ct);
}

// ---------- Role assignments (stored as Permify relationships, not in the database) ----------

public sealed record GetMembersQuery(string ResourceType, Guid ResourceId) : IRequest<IReadOnlyList<MemberDto>>, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceType, ResourceId.ToString(), Permissions.Manage)];
}

public sealed class GetMembersValidator : AbstractValidator<GetMembersQuery>
{
    public GetMembersValidator() =>
        RuleFor(x => x.ResourceType).Must(Relations.AssignableRoles.ContainsKey).WithMessage("Resource type has no assignable roles.");
}

public sealed class GetMembersHandler(IPermissionService permify) : IRequestHandler<GetMembersQuery, IReadOnlyList<MemberDto>>
{
    public async Task<IReadOnlyList<MemberDto>> Handle(GetMembersQuery r, CancellationToken ct)
    {
        var roles = Relations.AssignableRoles[r.ResourceType];
        var tuples = await permify.ReadRelationshipsAsync(r.ResourceType, r.ResourceId.ToString(), ct);
        return tuples
            .Where(t => t.SubjectType == ResourceTypes.User && roles.Contains(t.Relation))
            .Select(t => new MemberDto(t.SubjectId, t.Relation))
            .OrderBy(m => Array.IndexOf(roles, m.Role)).ThenBy(m => m.UserId)
            .ToList();
    }
}

public sealed record AssignRoleCommand(string ResourceType, Guid ResourceId, string UserId, string Role) : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceType, ResourceId.ToString(), Permissions.Manage)];
}

public sealed record RemoveRoleCommand(string ResourceType, Guid ResourceId, string UserId, string Role) : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceType, ResourceId.ToString(), Permissions.Manage)];
}

public sealed class AssignRoleValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(128).Matches("^[A-Za-z0-9_.@|-]+$");
        RuleFor(x => x.Role)
            .Must((x, role) => Relations.AssignableRoles.TryGetValue(x.ResourceType, out var roles) && roles.Contains(role))
            .WithMessage(x => $"'{x.Role}' is not an assignable role for {x.ResourceType}.");
    }
}

public sealed class RemoveRoleValidator : AbstractValidator<RemoveRoleCommand>
{
    public RemoveRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role)
            .Must((x, role) => Relations.AssignableRoles.TryGetValue(x.ResourceType, out var roles) && roles.Contains(role))
            .WithMessage(x => $"'{x.Role}' is not an assignable role for {x.ResourceType}.");
    }
}

public sealed class RoleCommandHandlers(IPermissionService permify) : IRequestHandler<AssignRoleCommand>, IRequestHandler<RemoveRoleCommand>
{
    public Task Handle(AssignRoleCommand r, CancellationToken ct) =>
        permify.WriteRelationshipsAsync([new(r.ResourceType, r.ResourceId.ToString(), r.Role, ResourceTypes.User, r.UserId)], ct);

    public Task Handle(RemoveRoleCommand r, CancellationToken ct) =>
        permify.DeleteRelationshipAsync(new(r.ResourceType, r.ResourceId.ToString(), r.Role, ResourceTypes.User, r.UserId), ct);
}
