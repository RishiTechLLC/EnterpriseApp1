# Database (grate migrations)

The School Calendar SQL Server / Azure SQL schema is managed by [grate](https://erikbra.github.io/grate/).
grate keeps a hash of every script it has run (in the `grate` schema of the target database). On each run it works out
what is new or changed and runs only that.

```
db/
├── grate.sh / grate.ps1      migrate · status · dryrun · watch · reset
└── sql/
    ├── up/            ONE-TIME   tables, columns, constraints, data fixes   (0001_..., 0002_..., run in name order)
    ├── functions/     ANYTIME    re-run whenever the file changes          (CREATE OR ALTER)
    ├── views/         ANYTIME
    ├── sprocs/        ANYTIME
    └── permissions/   EVERYTIME  run on every migration                   (must be idempotent)
```

grate runs the folders in this order: `up` → `functions` → `views` → `sprocs` → `permissions`.

| Object | Folder | Purpose |
|--------|--------|---------|
| `Schools`, `Programs`, `Calendars`, `CalendarEvents` | `up/0001-0004` | Tables used by the API through EF Core (cascade deletes, CHECK constraints for dates and enum values) |
| `fn_InstructionalDays` | `functions` | Weekdays in a calendar not covered by a non-instructional event (same rule as the domain model) |
| `vw_CalendarSummary`, `vw_CalendarEvents` | `views` | Reporting: calendars and events with school/program context |
| `usp_GetUpcomingEvents` | `sprocs` | Upcoming events across a school's published calendars |
| `usp_CopyCalendarEvents` | `sprocs` | Roll last year's events into a new calendar, shifting the dates |
| `schoolcalendar_app` role | `permissions` | Least-privilege role for the API (data + execute, no DDL) |

## Setup

```bash
dotnet tool restore            # installs grate (version pinned in /dotnet-tools.json)
```

A local SQL Server is included in `docker compose up` (`localhost,1433`, user `sa`, password `Local_Dev_Passw0rd!`).
That is also the scripts' default connection.

## Commands

| Bash | PowerShell | What it does |
|------|-----------|--------------|
| `./db/grate.sh migrate` | `./db/grate.ps1 migrate` | Creates the database if needed, then runs new `up` scripts, changed functions/views/sprocs, and `permissions`, all in one transaction. Safe to run any number of times. |
| `./db/grate.sh status` | `./db/grate.ps1 status` | Detects changes: exit **0** = up to date, **2** = scripts pending, **1** = error |
| `./db/grate.sh dryrun` | `./db/grate.ps1 dryrun` | Logs what would run without changing anything |
| `./db/grate.sh watch` | `./db/grate.ps1 watch` | Local dev: migrates, then re-migrates whenever any `.sql` file is added, edited or removed |
| `./db/grate.sh reset` | `./db/grate.ps1 reset` | Drops and rebuilds the database (only when `DB_ENV=LOCAL`) |

Settings come from environment variables:

| Variable | Default |
|----------|---------|
| `DB_CONNECTION` | local docker SQL Server |
| `DB_ENV` | `LOCAL` |
| `DB_TYPE` | `sqlserver` |
| `DB_SQL_DIR` | `db/sql` |
| `WATCH_INTERVAL` | `2` seconds |

```bash
# Azure SQL with your Entra ID login (az login first):
DB_CONNECTION="Server=tcp:<server>.database.windows.net,1433;Database=SchoolCalendar;Authentication=Active Directory Default;Encrypt=True" \
DB_ENV=PROD ./db/grate.sh migrate
```

Calling grate directly does the same thing:
```bash
dotnet tool run grate -- --connectionstring "<conn>" --files db/sql --databasetype sqlserver --environment LOCAL --transaction --silent
```

## How changes are detected

| You… | grate on the next `migrate`… |
|------|------------------------------|
| add `up/0005_add_column.sql` | runs it once and records it |
| edit a view, function or procedure | sees the new hash and re-runs it (`CREATE OR ALTER`) |
| edit a `permissions` script | runs it (these always run) |
| **edit an `up` script that already ran** | **fails**: `One time script changed`. Deployed one-time scripts are history; add a new numbered script instead |

While you're developing a new `up` script locally, run `./db/grate.sh reset` to rebuild the local database after editing it.

## Making a schema change

1. Change the entity in `backend/src/SchoolCalendar.Domain` and its mapping in `AppDbContext`.
2. Add `db/sql/up/000N_<what>.sql`, e.g.:
   ```sql
   ALTER TABLE dbo.Schools ADD Phone NVARCHAR(30) NULL;
   ```
3. Run `dotnet test backend`. `DatabaseScriptTests` replays the `up` scripts and fails if the tables don't exactly match
   the EF model (column names, types, lengths, nullability). It also parses every script as T-SQL, checks that anytime
   scripts use `CREATE OR ALTER`, and checks that the CHECK constraints allow every enum value.
4. Run `./db/grate.sh migrate` (or `watch`).
5. On merge to `main`, `.github/workflows/database.yml` applies the change. On pull requests it migrates a throwaway
   SQL Server twice, to prove the scripts are re-runnable.

**Environment-specific scripts:** `0006_demo_data.ENV.LOCAL.sql` runs only with `DB_ENV=LOCAL`.

## API configuration

```json
"Database": { "Provider": "SqlServer" },
"ConnectionStrings": { "Database": "Server=...;Database=SchoolCalendar;..." }
```
With `SqlServer` the API never creates or changes the schema. At startup it checks that the tables exist and tells you to run grate if they don't.
The default `Sqlite` provider is still available for zero-setup local runs, with the schema created by EF.

For Azure SQL with a managed identity, add the API's identity to the role once:
```sql
CREATE USER [app-schoolcalendar-api] FROM EXTERNAL PROVIDER;
ALTER ROLE schoolcalendar_app ADD MEMBER [app-schoolcalendar-api];
```
