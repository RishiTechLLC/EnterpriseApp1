namespace SchoolCalendar.Application.Common.Authorization;

/// <summary>Entity types declared in the Permify schema (permify/schema.perm).</summary>
public static class ResourceTypes
{
    public const string User = "user";
    public const string Organization = "organization";
    public const string School = "school";
    public const string Program = "program";
    public const string Calendar = "calendar";
}

/// <summary>Permission names declared in the Permify schema.</summary>
public static class Permissions
{
    public const string CreateSchool = "create_school";
    public const string View = "view";
    public const string Edit = "edit";
    public const string Manage = "manage";
    public const string Publish = "publish";
    public const string Delete = "delete";
}

/// <summary>Relations declared in the Permify schema.</summary>
public static class Relations
{
    public const string Org = "org";
    public const string School = "school";
    public const string Program = "program";
    public const string Admin = "admin";
    public const string Staff = "staff";
    public const string Viewer = "viewer";
    public const string Coordinator = "coordinator";

    /// <summary>Roles that may be granted to users directly, per resource type.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> AssignableRoles = new Dictionary<string, string[]>
    {
        [ResourceTypes.School] = [Admin, Staff, Viewer],
        [ResourceTypes.Program] = [Coordinator],
    };
}

/// <summary>Attributes declared in the Permify schema (ABAC).</summary>
public static class Attributes
{
    public const string IsPublished = "is_published";
}

public sealed record PermissionRequirement(string EntityType, string EntityId, string Permission);

public sealed record RelationshipTuple(string EntityType, string EntityId, string Relation, string SubjectType, string SubjectId);

/// <summary>
/// Implemented by every command/query that targets a protected resource.
/// <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/> checks every requirement against Permify
/// before the handler runs.
/// </summary>
public interface IAuthorizedRequest
{
    IEnumerable<PermissionRequirement> GetPermissionRequirements();
}

/// <summary>
/// Marker for requests whose handler authorizes by asking Permify which resources the user can access
/// (lookup-entity) rather than checking a single resource, e.g. "list my schools".
/// </summary>
public interface IAuthorizedByLookup;

/// <summary>Client for the Permify authorization service.</summary>
public interface IPermissionService
{
    Task<bool> CheckAsync(PermissionRequirement requirement, string userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the ids of every <paramref name="entityType"/> on which the user has <paramref name="permission"/>.</summary>
    Task<IReadOnlyList<string>> LookupEntityIdsAsync(string entityType, string permission, string userId, CancellationToken cancellationToken = default);

    Task WriteRelationshipsAsync(IEnumerable<RelationshipTuple> tuples, CancellationToken cancellationToken = default);
    Task DeleteRelationshipAsync(RelationshipTuple tuple, CancellationToken cancellationToken = default);

    /// <summary>Deletes every relationship and attribute whose entity is one of <paramref name="entityIds"/>.</summary>
    Task DeleteEntityDataAsync(string entityType, IEnumerable<string> entityIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RelationshipTuple>> ReadRelationshipsAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
    Task WriteBooleanAttributeAsync(string entityType, string entityId, string attribute, bool value, CancellationToken cancellationToken = default);
}
