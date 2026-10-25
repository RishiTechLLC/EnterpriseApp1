using Microsoft.EntityFrameworkCore;
using SchoolCalendar.Domain.Entities;

namespace SchoolCalendar.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<School> Schools { get; }
    DbSet<AcademicProgram> Programs { get; }
    DbSet<AcademicCalendar> Calendars { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    /// <summary>The authenticated user's id (JWT "sub"); null for anonymous callers.</summary>
    string? UserId { get; }
}

/// <summary>
/// Tag-aware cache. Entries are stored under a key and associated with one or more tags;
/// invalidating a tag makes every entry carrying it unreachable.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, IReadOnlyCollection<string> tags, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task InvalidateTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}
