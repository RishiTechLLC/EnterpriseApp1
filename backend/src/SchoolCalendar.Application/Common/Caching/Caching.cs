namespace SchoolCalendar.Application.Common.Caching;

/// <summary>
/// Queries implementing this are served from cache by <see cref="Behaviors.CachingBehavior{TRequest,TResponse}"/>.
/// Authorization always runs first, so a cached response is only returned to callers who pass the Permify check.
/// Cached responses must therefore not depend on who the caller is.
/// </summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
    IReadOnlyCollection<string> CacheTags { get; }
    TimeSpan? CacheDuration => null;
}

public static class CacheTags
{
    public static string School(Guid id) => $"school:{id}";
    public static string Program(Guid id) => $"program:{id}";
    public static string Calendar(Guid id) => $"calendar:{id}";
}
