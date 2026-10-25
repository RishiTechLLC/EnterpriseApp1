using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolCalendar.Application.Common.Authorization;

namespace SchoolCalendar.Infrastructure.Permify;

public sealed class PermifyOptions
{
    public const string Section = "Permify";

    /// <summary>Permify REST endpoint, e.g. http://localhost:3476.</summary>
    public string BaseUrl { get; set; } = "http://localhost:3476";
    public string TenantId { get; set; } = "t1";
    /// <summary>Pre-shared key when Permify runs with authn enabled; leave empty otherwise.</summary>
    public string? ApiKey { get; set; }
    public int Depth { get; set; } = 20;
    public string SchemaFile { get; set; } = "permify/schema.perm";
    /// <summary>Organization (district) that schools are created under.</summary>
    public string OrganizationId { get; set; } = "district-1";
}

/// <summary>Thin client over the Permify v1 REST API.</summary>
public sealed class PermifyClient(HttpClient http, IOptions<PermifyOptions> options, ILogger<PermifyClient> logger) : IPermissionService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly PermifyOptions _options = options.Value;
    private string Tenant => Uri.EscapeDataString(_options.TenantId);

    public async Task<bool> CheckAsync(PermissionRequirement requirement, string userId, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            metadata = new { snap_token = "", schema_version = "", depth = _options.Depth },
            entity = new { type = requirement.EntityType, id = requirement.EntityId },
            permission = requirement.Permission,
            subject = new { type = ResourceTypes.User, id = userId },
        };
        var result = await PostAsync<CheckResponse>($"/v1/tenants/{Tenant}/permissions/check", body, cancellationToken);
        return result?.Can == "CHECK_RESULT_ALLOWED";
    }

    public async Task<IReadOnlyList<string>> LookupEntityIdsAsync(string entityType, string permission, string userId, CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        var token = "";
        do
        {
            var body = new
            {
                metadata = new { snap_token = "", schema_version = "", depth = _options.Depth },
                entity_type = entityType,
                permission,
                subject = new { type = ResourceTypes.User, id = userId },
                page_size = 100,
                continuous_token = token,
            };
            var result = await PostAsync<LookupEntityResponse>($"/v1/tenants/{Tenant}/permissions/lookup-entity", body, cancellationToken);
            ids.AddRange(result?.EntityIds ?? []);
            token = result?.ContinuousToken ?? "";
        } while (!string.IsNullOrEmpty(token));
        return ids;
    }

    public Task WriteRelationshipsAsync(IEnumerable<RelationshipTuple> tuples, CancellationToken cancellationToken = default)
    {
        var list = tuples.Select(t => new
        {
            entity = new { type = t.EntityType, id = t.EntityId },
            relation = t.Relation,
            subject = new { type = t.SubjectType, id = t.SubjectId },
        }).ToList();
        if (list.Count == 0) return Task.CompletedTask;

        var body = new { metadata = new { schema_version = "" }, tuples = list, attributes = Array.Empty<object>() };
        return PostAsync<JsonElement>($"/v1/tenants/{Tenant}/data/write", body, cancellationToken);
    }

    public Task WriteBooleanAttributeAsync(string entityType, string entityId, string attribute, bool value, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            metadata = new { schema_version = "" },
            tuples = Array.Empty<object>(),
            attributes = new[]
            {
                new
                {
                    entity = new { type = entityType, id = entityId },
                    attribute,
                    value = new Dictionary<string, object> { ["@type"] = "type.googleapis.com/base.v1.BooleanValue", ["data"] = value },
                },
            },
        };
        return PostAsync<JsonElement>($"/v1/tenants/{Tenant}/data/write", body, cancellationToken);
    }

    public Task DeleteRelationshipAsync(RelationshipTuple tuple, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            tuple_filter = new
            {
                entity = new { type = tuple.EntityType, ids = new[] { tuple.EntityId } },
                relation = tuple.Relation,
                subject = new { type = tuple.SubjectType, ids = new[] { tuple.SubjectId }, relation = "" },
            },
            attribute_filter = new { },
        };
        return PostAsync<JsonElement>($"/v1/tenants/{Tenant}/data/delete", body, cancellationToken);
    }

    public Task DeleteEntityDataAsync(string entityType, IEnumerable<string> entityIds, CancellationToken cancellationToken = default)
    {
        var ids = entityIds.ToArray();
        if (ids.Length == 0) return Task.CompletedTask;

        var body = new
        {
            tuple_filter = new
            {
                entity = new { type = entityType, ids },
                relation = "",
                subject = new { type = "", ids = Array.Empty<string>(), relation = "" },
            },
            attribute_filter = new { entity = new { type = entityType, ids }, attributes = Array.Empty<string>() },
        };
        return PostAsync<JsonElement>($"/v1/tenants/{Tenant}/data/delete", body, cancellationToken);
    }

    public async Task<IReadOnlyList<RelationshipTuple>> ReadRelationshipsAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        var tuples = new List<RelationshipTuple>();
        var token = "";
        do
        {
            var body = new
            {
                metadata = new { snap_token = "" },
                filter = new
                {
                    entity = new { type = entityType, ids = new[] { entityId } },
                    relation = "",
                    subject = new { type = "", ids = Array.Empty<string>(), relation = "" },
                },
                page_size = 100,
                continuous_token = token,
            };
            var result = await PostAsync<ReadRelationshipsResponse>($"/v1/tenants/{Tenant}/data/relationships/read", body, cancellationToken);
            tuples.AddRange((result?.Tuples ?? []).Select(t =>
                new RelationshipTuple(t.Entity.Type, t.Entity.Id, t.Relation, t.Subject.Type, t.Subject.Id)));
            token = result?.ContinuousToken ?? "";
        } while (!string.IsNullOrEmpty(token));
        return tuples;
    }

    public async Task<string?> WriteSchemaAsync(string schema, CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<SchemaWriteResponse>($"/v1/tenants/{Tenant}/schemas/write", new { schema }, cancellationToken);
        return result?.SchemaVersion;
    }

    private async Task<T?> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(path, body, Json, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Permify {Path} failed with {Status}: {Error}", path, (int)response.StatusCode, error);
            throw new PermifyException($"Permify call {path} failed with {(int)response.StatusCode}: {error}");
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    private sealed record CheckResponse(string? Can);
    private sealed record LookupEntityResponse(
        [property: JsonPropertyName("entity_ids")] List<string>? EntityIds,
        [property: JsonPropertyName("continuous_token")] string? ContinuousToken);
    private sealed record ReadRelationshipsResponse(
        List<TupleDto>? Tuples,
        [property: JsonPropertyName("continuous_token")] string? ContinuousToken);
    private sealed record TupleDto(EntityDto Entity, string Relation, EntityDto Subject);
    private sealed record EntityDto(string Type, string Id);
    private sealed record SchemaWriteResponse([property: JsonPropertyName("schema_version")] string? SchemaVersion);
}

/// <summary>Permify unreachable or returned an error. Mapped to HTTP 503: access is denied when the authorizer is down.</summary>
public sealed class PermifyException(string message) : Exception(message);
