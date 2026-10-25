namespace SchoolCalendar.Infrastructure.Persistence;

public enum DatabaseProvider
{
    Sqlite,
    SqlServer
}

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>
    /// SqlServer: the schema is created and evolved by grate (see /db) and the API never changes it.
    /// Sqlite: local development only; the schema is created from the EF model on startup.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
}
