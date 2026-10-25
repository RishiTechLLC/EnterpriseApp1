using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SchoolCalendar.Api.Infrastructure;
using SchoolCalendar.Application;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Infrastructure;
using SchoolCalendar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.Section));
var jwt = builder.Configuration.GetSection(JwtSettings.Section).Get<JwtSettings>() ?? new JwtSettings();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        if (!string.IsNullOrWhiteSpace(jwt.Authority))
        {
            // Production: tokens from an external identity provider (Entra ID, Auth0, Keycloak, ...).
            o.Authority = jwt.Authority;
            o.Audience = jwt.Audience;
        }
        else
        {
            // Development: tokens minted by /api/auth/dev-token with a symmetric key.
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                NameClaimType = "name",
            };
        }
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
        .InitializeAsync(seedDemoData: app.Configuration.GetValue("SeedDemoData", app.Environment.IsDevelopment()));
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
