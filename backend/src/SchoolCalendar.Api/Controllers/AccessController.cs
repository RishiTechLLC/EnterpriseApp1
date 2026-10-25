using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SchoolCalendar.Application.Features;
using SchoolCalendar.Application.Features.Access;
using SchoolCalendar.Infrastructure.Permify;

namespace SchoolCalendar.Api.Controllers;

/// <summary>Permission introspection and role management, backed by Permify.</summary>
[ApiController]
[Route("api/access")]
public sealed class AccessController(ISender sender, IOptions<PermifyOptions> permify) : ControllerBase
{
    public sealed record AssignRoleRequest(string UserId, string Role);

    [HttpGet("can-create-school")]
    public Task<bool> CanCreateSchool(CancellationToken ct) => sender.Send(new CanCreateSchoolQuery(permify.Value.OrganizationId), ct);

    /// <param name="resourceType">school, program or calendar</param>
    [HttpGet("{resourceType}/{resourceId:guid}/permissions")]
    public Task<IReadOnlyDictionary<string, bool>> GetPermissions(string resourceType, Guid resourceId, CancellationToken ct) =>
        sender.Send(new GetMyPermissionsQuery(resourceType, resourceId), ct);

    [HttpGet("{resourceType}/{resourceId:guid}/members")]
    public Task<IReadOnlyList<MemberDto>> GetMembers(string resourceType, Guid resourceId, CancellationToken ct) =>
        sender.Send(new GetMembersQuery(resourceType, resourceId), ct);

    [HttpPost("{resourceType}/{resourceId:guid}/members")]
    public async Task<IActionResult> AssignRole(string resourceType, Guid resourceId, AssignRoleRequest r, CancellationToken ct)
    {
        await sender.Send(new AssignRoleCommand(resourceType, resourceId, r.UserId, r.Role), ct);
        return NoContent();
    }

    [HttpDelete("{resourceType}/{resourceId:guid}/members/{role}/{userId}")]
    public async Task<IActionResult> RemoveRole(string resourceType, Guid resourceId, string role, string userId, CancellationToken ct)
    {
        await sender.Send(new RemoveRoleCommand(resourceType, resourceId, userId, role), ct);
        return NoContent();
    }
}
