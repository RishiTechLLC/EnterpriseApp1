using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SchoolCalendar.Application.Common.Interfaces;

namespace SchoolCalendar.Infrastructure.Caching;

/// <summary>
/// Tag-based invalidation on top of <see cref="IDistributedCache"/> (Redis in production, in-memory locally).
/// Each tag has a version token; an entry's physical key embeds the current token of every tag it carries.
/// Invalidating a tag rotates its token, so stale entries are never read again and simply expire.
/// Works across API instances because the tokens live in the shared cache.
/// </summary>
public sealed class DistributedCacheService(IDistributedCache cache, ILogger<DistributedCacheService> logger) : ICacheService
{
    private const string Prefix = "sc:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DistributedCacheEntryOptions TagTokenOptions = new() { SlidingExpiration = TimeSpan.FromDays(7) };

    public async Task<T?> GetAsync<T>(string key, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await cache.GetAsync(await PhysicalKeyAsync(key, tags, cancellationToken), cancellationToken);
            return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, Json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A cache outage must never take the API down: fall through to the database.
            logger.LogWarning(ex, "Cache read failed for {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, IReadOnlyCollection<string> tags, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
            await cache.SetAsync(await PhysicalKeyAsync(key, tags, cancellationToken), bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10) },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }
    }

    public async Task InvalidateTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        foreach (var tag in tags.Distinct())
        {
            // Unlike reads, a failed invalidation is surfaced: silently serving stale data after a write is worse than an error.
            await cache.SetStringAsync(TagKey(tag), Guid.NewGuid().ToString("N"), TagTokenOptions, cancellationToken);
            logger.LogDebug("Invalidated cache tag {Tag}", tag);
        }
    }

    private async Task<string> PhysicalKeyAsync(string key, IReadOnlyCollection<string> tags, CancellationToken cancellationToken)
    {
        if (tags.Count == 0) return Prefix + key;

        var tokens = new List<string>(tags.Count);
        foreach (var tag in tags.Order(StringComparer.Ordinal))
        {
            var token = await cache.GetStringAsync(TagKey(tag), cancellationToken);
            if (token is null)
            {
                token = Guid.NewGuid().ToString("N");
                await cache.SetStringAsync(TagKey(tag), token, TagTokenOptions, cancellationToken);
            }
            tokens.Add(token);
        }
        return $"{Prefix}{key}|{string.Join('.', tokens)}";
    }

    private static string TagKey(string tag) => $"{Prefix}tag:{tag}";
}
