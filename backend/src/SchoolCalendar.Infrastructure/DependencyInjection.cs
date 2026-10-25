using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Infrastructure.Caching;
using SchoolCalendar.Infrastructure.Permify;
using SchoolCalendar.Infrastructure.Persistence;

namespace SchoolCalendar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // SqlServer: schema is owned by the grate migrations in /db. Sqlite: zero-setup local dev, schema created by EF.
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.Section));
        var provider = configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>()?.Provider ?? DatabaseProvider.Sqlite;
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<AppDbContext>(o =>
        {
            if (provider == DatabaseProvider.SqlServer)
                o.UseSqlServer(connectionString ?? throw new InvalidOperationException("ConnectionStrings:Database is required for SQL Server."),
                    sql => sql.EnableRetryOnFailure());
            else
                o.UseSqlite(connectionString ?? "Data Source=schoolcalendar.db");
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DatabaseInitializer>();

        // Caching: Redis when configured (shared across API instances), otherwise in-process memory.
        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
            services.AddStackExchangeRedisCache(o => { o.Configuration = redis; o.InstanceName = "school-calendar:"; });
        else
            services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();

        // Permify
        services.Configure<PermifyOptions>(configuration.GetSection(PermifyOptions.Section));
        services.AddHttpClient<PermifyClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<PermifyOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(5);
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        });
        services.AddTransient<IPermissionService>(sp => sp.GetRequiredService<PermifyClient>());

        return services;
    }
}
