# School Calendar

Manage academic calendars for schools, where **each school runs separate calendars for each of its programs**
(for example Elementary, Middle School, High School, IB Diploma or Adult Education).

| Layer     | Tech |
|-----------|------|
| Frontend  | Angular 21 (standalone components, signals, lazy routes) |
| Backend   | .NET 10 Web API, **CQRS** with MediatR + FluentValidation, EF Core |
| Database  | SQL Server / Azure SQL, schema managed by **[grate](https://erikbra.github.io/grate/)** migrations in `db/` (SQLite for zero-setup local runs) |
| AuthZ     | **[Permify](https://permify.co)**: every API call is checked (ReBAC + ABAC) |
| Caching   | Tag-invalidated cache on `IDistributedCache`: **Redis** when configured, in-memory otherwise |

```
School ─┬─ Program (Elementary)  ─── Calendar 2026-2027 ─── Events (holidays, breaks, exams, PD days, …)
        ├─ Program (Middle)      ─── Calendar 2026-2027 (draft)
        └─ Program (High School) ─── Calendar 2026-2027
```

## Quick start

### Docker (everything)
```bash
docker compose up --build
# http://localhost:8080 → pick a demo user
```
This starts Angular (nginx), the API, SQL Server (migrated by grate before the API starts), Permify (with Postgres), and Redis.

### Local development
```bash
# 1. Permify (in-memory)
docker run -p 3476:3476 -p 3478:3478 ghcr.io/permify/permify:v1.7.5 serve

# 2. API (http://localhost:5157). Writes the Permify schema and seeds demo data on startup.
cd backend && dotnet run --project src/SchoolCalendar.Api --launch-profile http

# 3. Angular (http://localhost:4200). /api is proxied to the API.
cd frontend && npm install && npm start
```
Optional: `ConnectionStrings__Redis=localhost:6379` makes the API cache in Redis.

To use SQL Server instead of SQLite, see **[db/README.md](db/README.md)**:
```bash
dotnet tool restore && ./db/grate.sh migrate     # or: ./db/grate.sh watch  (re-runs on every .sql change)
Database__Provider=SqlServer ConnectionStrings__Database="Server=localhost,1433;Database=SchoolCalendar;User Id=sa;Password=Local_Dev_Passw0rd!;TrustServerCertificate=True" \
  dotnet run --project backend/src/SchoolCalendar.Api --launch-profile http
```

### Tests
```bash
cd backend && dotnet test          # behaviors, caching, domain rules, SQL scripts vs EF model
cd frontend && npx ng test --watch=false
```

## Demo users (Development)

The login page issues a dev JWT for any user id. Seeded Permify relationships:

| User | Relationship | Can do |
|------|--------------|--------|
| `district-admin` | `organization:district-1#admin` | Everything; create schools |
| `principal` | `school:Lincoln#admin` | Manage Lincoln: programs, calendars, publishing, roles |
| `coordinator` | `program:HighSchool#coordinator` + Lincoln staff | Edit High School calendars; **cannot publish** |
| `teacher` | `school:Lincoln#staff` | View **published** Lincoln calendars |
| `parent` | `school:Lincoln#viewer` | View **published** Lincoln calendars |
| `riverside-admin` | `school:Riverside#admin` | Riverside only; Lincoln returns 403 |

## Architecture

```
backend/src
├── SchoolCalendar.Domain          Entities + business rules (event must fall in calendar range, publish/unpublish, instructional-day count)
├── SchoolCalendar.Application     CQRS: Features/{Schools,Programs,Calendars,Events,Access}
│   └── Common/Behaviors           Pipeline: Validation → Authorization (Permify) → Caching → Handler
├── SchoolCalendar.Infrastructure  EF Core, PermifyClient (REST), DistributedCacheService, startup seeding/sync
└── SchoolCalendar.Api             Thin controllers → ISender, JWT auth, ProblemDetails error mapping
db/sql                             grate migrations: up (tables), functions, views, sprocs, permissions
permify/schema.perm                Authorization model
frontend/src/app                   core/ (api, auth, models) · features/ (pages) · shared/ (forms, modal, members)
```

### CQRS
Each use case is a command or query record with its own handler, for example `CreateCalendarCommand` or
`GetCalendarByIdQuery`. Controllers only translate HTTP into `sender.Send(...)`.
Commands change state and invalidate cache tags. Queries are read-only (`AsNoTracking`) and can be cached.

### Authorization with Permify
`permify/schema.perm` models `organization → school → program → calendar`:

```
school:   manage = admin or org.admin     view = manage or staff or viewer
program:  manage = school.manage          edit = manage or coordinator     view = edit or school.view
calendar: edit = program.edit             publish/delete = program.manage
          view = edit or (program.view and is_published)        ← ABAC: drafts hidden from viewers
```

**Every request is checked.** Each command or query declares what it needs:

```csharp
public sealed record UpdateCalendarCommand(Guid CalendarId, ...) : IRequest, IAuthorizedRequest
{
    public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
        [new(ResourceTypes.Calendar, CalendarId.ToString(), Permissions.Edit)];
}
```

`AuthorizationBehavior` calls Permify's `permissions/check` before the handler runs. A request that declares
neither `IAuthorizedRequest` nor `IAuthorizedByLookup` is **rejected at runtime**, and a unit test also fails
the build for it. List endpoints such as "my schools" use Permify `lookup-entity`.
Handlers keep Permify in sync: creating a school/program/calendar writes its parent relationship,
publishing writes the `is_published` attribute, and deletes remove the entity's tuples.
Role assignment (`/api/access/{type}/{id}/members`) writes relationships directly.
If Permify is unreachable, the API returns **503** and never allows access.

### Caching
Queries implementing `ICacheableQuery` are served by `CachingBehavior`. It runs **after** authorization,
so cached data is only returned to callers Permify has just allowed.

- Entries carry **tags** (`school:{id}`, `program:{id}`, `calendar:{id}`). Each tag has a version token stored in the cache.
  An entry's key includes the current tokens, so a command that rotates a tag makes every dependent entry unreachable,
  across all API instances.
- The program calendar list caches the full list once, then filters out drafts per caller (so a cached response never leaks drafts).
- Cache failures on read/write degrade to the database.

## API overview

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/api/schools` | lookup `school.view` |
| POST | `/api/schools` | `organization.create_school` |
| GET/PUT/DELETE | `/api/schools/{id}` | `view` / `edit` / `manage` |
| GET/POST | `/api/schools/{id}/programs` | `school.view` / `school.manage` |
| GET/PUT/DELETE | `/api/programs/{id}` | `view` / `manage` / `manage` |
| GET/POST | `/api/programs/{id}/calendars` | `program.view` / `program.edit` |
| GET/PUT/DELETE | `/api/calendars/{id}` | `view` / `edit` / `delete` |
| POST | `/api/calendars/{id}/publish` · `/unpublish` | `calendar.publish` |
| GET | `/api/calendars/{id}/export.ics` | `calendar.view` |
| GET/POST | `/api/calendars/{id}/events` | `calendar.view` / `calendar.edit` |
| PUT/DELETE | `/api/calendars/{id}/events/{eventId}` | `calendar.edit` |
| GET | `/api/access/{type}/{id}/permissions` | `view` (drives which buttons the UI shows) |
| GET/POST/DELETE | `/api/access/{type}/{id}/members` | `manage` |

## Production notes
- Set `Jwt:Authority`/`Jwt:Audience` to your identity provider (Entra ID, Auth0, Keycloak). Dev tokens are then disabled.
  The JWT `sub` must match the user ids used in Permify relationships.
- Run Permify with Postgres and set `Permify:ApiKey` if Permify authn is enabled.
- Use `Database:Provider=SqlServer` (Azure SQL) and run `db/grate.sh migrate` in your deployment pipeline (see `.github/workflows/database.yml`).
