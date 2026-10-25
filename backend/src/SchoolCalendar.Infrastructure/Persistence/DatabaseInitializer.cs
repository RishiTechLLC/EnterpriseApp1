using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Domain.Entities;
using SchoolCalendar.Domain.Enums;
using SchoolCalendar.Infrastructure.Permify;

namespace SchoolCalendar.Infrastructure.Persistence;

/// <summary>
/// Runs at startup: writes the Permify schema, creates the database, seeds demo data on first run,
/// and re-syncs the structural relationships (school→org, program→school, calendar→program) from the database
/// so a restarted Permify never loses the resource hierarchy.
/// </summary>
public sealed class DatabaseInitializer(
    AppDbContext db,
    PermifyClient permify,
    IOptions<PermifyOptions> options,
    ILogger<DatabaseInitializer> logger)
{
    private readonly PermifyOptions _options = options.Value;

    public async Task InitializeAsync(bool seedDemoData, CancellationToken ct = default)
    {
        await WriteSchemaWithRetryAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);

        if (seedDemoData && !await db.Schools.AnyAsync(ct))
            await SeedDemoDataAsync(ct);

        await SyncHierarchyAsync(ct);
    }

    private async Task WriteSchemaWithRetryAsync(CancellationToken ct)
    {
        var path = Path.IsPathRooted(_options.SchemaFile) ? _options.SchemaFile : Path.Combine(AppContext.BaseDirectory, _options.SchemaFile);
        var schema = await File.ReadAllTextAsync(path, ct);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var version = await permify.WriteSchemaAsync(schema, ct);
                logger.LogInformation("Permify schema written (version {Version})", version);
                return;
            }
            catch (Exception ex) when (attempt < 10 && ex is HttpRequestException or PermifyException)
            {
                logger.LogWarning("Permify not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 2, 10)), ct);
            }
        }
    }

    private async Task SyncHierarchyAsync(CancellationToken ct)
    {
        var schools = await db.Schools.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        var programs = await db.Programs.AsNoTracking().Select(p => new { p.Id, p.SchoolId }).ToListAsync(ct);
        var calendars = await db.Calendars.AsNoTracking().Select(c => new { c.Id, c.ProgramId, c.Status }).ToListAsync(ct);

        var tuples = schools.Select(id => new RelationshipTuple(ResourceTypes.School, id.ToString(), Relations.Org, ResourceTypes.Organization, _options.OrganizationId))
            .Concat(programs.Select(p => new RelationshipTuple(ResourceTypes.Program, p.Id.ToString(), Relations.School, ResourceTypes.School, p.SchoolId.ToString())))
            .Concat(calendars.Select(c => new RelationshipTuple(ResourceTypes.Calendar, c.Id.ToString(), Relations.Program, ResourceTypes.Program, c.ProgramId.ToString())));

        foreach (var batch in tuples.Chunk(100))
            await permify.WriteRelationshipsAsync(batch, ct);
        foreach (var c in calendars)
            await permify.WriteBooleanAttributeAsync(ResourceTypes.Calendar, c.Id.ToString(), Attributes.IsPublished, c.Status == CalendarStatus.Published, ct);

        logger.LogInformation("Synced Permify hierarchy: {Schools} schools, {Programs} programs, {Calendars} calendars",
            schools.Count, programs.Count, calendars.Count);
    }

    private async Task SeedDemoDataAsync(CancellationToken ct)
    {
        logger.LogInformation("Seeding demo data");

        var lincoln = new School("Lincoln Unified K-12", "LUK12", "100 Lincoln Ave, Springfield", "America/Chicago", "seed");
        var riverside = new School("Riverside Academy", "RVA", "42 River Rd, Springfield", "America/Chicago", "seed");

        var elementary = new AcademicProgram(lincoln.Id, "Elementary School", "ES", "Kindergarten through 5th grade", "K-5");
        var middle = new AcademicProgram(lincoln.Id, "Middle School", "MS", "6th through 8th grade", "6-8");
        var high = new AcademicProgram(lincoln.Id, "High School", "HS", "9th through 12th grade", "9-12");
        var ib = new AcademicProgram(riverside.Id, "IB Diploma Programme", "IBDP", "International Baccalaureate, two-year diploma", "11-12");
        var adult = new AcademicProgram(riverside.Id, "Adult Education", "ADULT", "Evening classes for adult learners", null);

        var calendars = new List<AcademicCalendar>
        {
            DistrictCalendar(elementary, "Elementary 2026-2027", new(2026, 8, 19), new(2027, 6, 4), c =>
            {
                c.AddEvent("Parent-Teacher Conferences", null, CalendarEventType.ParentTeacherConference, new(2026, 10, 22), new(2026, 10, 23), false);
                c.AddEvent("Field Day", null, CalendarEventType.SchoolEvent, new(2027, 5, 21), new(2027, 5, 21), true);
            }),
            DistrictCalendar(middle, "Middle School 2026-2027", new(2026, 8, 19), new(2027, 6, 4), c =>
            {
                c.AddEvent("Early Dismissal - Staff Meetings", "Dismissal at 1:00 PM", CalendarEventType.EarlyDismissal, new(2026, 9, 30), new(2026, 9, 30), true);
                c.AddEvent("State Assessments", null, CalendarEventType.Exam, new(2027, 4, 12), new(2027, 4, 16), true);
            }, publish: false),
            DistrictCalendar(high, "High School 2026-2027", new(2026, 8, 17), new(2027, 6, 2), c =>
            {
                c.AddEvent("Fall Semester Finals", null, CalendarEventType.Exam, new(2026, 12, 15), new(2026, 12, 18), true);
                c.AddEvent("Spring Semester Finals", null, CalendarEventType.Exam, new(2027, 5, 25), new(2027, 5, 28), true);
                c.AddEvent("Graduation", "Lincoln Stadium, 7:00 PM", CalendarEventType.SchoolEvent, new(2027, 6, 2), new(2027, 6, 2), true);
            }),
            Calendar(ib, "IB Diploma 2026-2027", new(2026, 9, 1), new(2027, 6, 18), c =>
            {
                c.AddEvent("Term 1 Begins", null, CalendarEventType.TermStart, new(2026, 9, 1), new(2026, 9, 1), true);
                c.AddEvent("Autumn Half Term", null, CalendarEventType.Break, new(2026, 10, 26), new(2026, 10, 30), false);
                c.AddEvent("IB May Examinations", null, CalendarEventType.Exam, new(2027, 4, 30), new(2027, 5, 21), true);
                c.AddEvent("Term 3 Ends", null, CalendarEventType.TermEnd, new(2027, 6, 18), new(2027, 6, 18), true);
            }),
            Calendar(adult, "Adult Education Fall 2026", new(2026, 9, 8), new(2026, 12, 17), c =>
            {
                c.AddEvent("Classes Begin", null, CalendarEventType.TermStart, new(2026, 9, 8), new(2026, 9, 8), true);
                c.AddEvent("Thanksgiving - No Classes", null, CalendarEventType.Holiday, new(2026, 11, 26), new(2026, 11, 26), false);
                c.AddEvent("Classes End", null, CalendarEventType.TermEnd, new(2026, 12, 17), new(2026, 12, 17), true);
            }),
        };

        db.Schools.AddRange(lincoln, riverside);
        db.Programs.AddRange(elementary, middle, high, ib, adult);
        db.Calendars.AddRange(calendars);
        await db.SaveChangesAsync(ct);

        // Demo users (sign in as any of them from the login page in Development).
        await permify.WriteRelationshipsAsync(
        [
            new(ResourceTypes.Organization, _options.OrganizationId, Relations.Admin, ResourceTypes.User, "district-admin"),
            new(ResourceTypes.School, lincoln.Id.ToString(), Relations.Admin, ResourceTypes.User, "principal"),
            new(ResourceTypes.School, lincoln.Id.ToString(), Relations.Staff, ResourceTypes.User, "teacher"),
            new(ResourceTypes.School, lincoln.Id.ToString(), Relations.Viewer, ResourceTypes.User, "parent"),
            new(ResourceTypes.School, lincoln.Id.ToString(), Relations.Staff, ResourceTypes.User, "coordinator"),
            new(ResourceTypes.Program, high.Id.ToString(), Relations.Coordinator, ResourceTypes.User, "coordinator"),
            new(ResourceTypes.School, riverside.Id.ToString(), Relations.Admin, ResourceTypes.User, "riverside-admin"),
        ], ct);
    }

    private static AcademicCalendar Calendar(AcademicProgram program, string name, DateOnly start, DateOnly end,
        Action<AcademicCalendar> addEvents, bool publish = true)
    {
        var calendar = new AcademicCalendar(program.Id, name, $"{start.Year}-{start.Year + 1}", start, end, null);
        addEvents(calendar);
        if (publish) calendar.Publish();
        return calendar;
    }

    /// <summary>A calendar pre-filled with the holidays shared by every program in the district.</summary>
    private static AcademicCalendar DistrictCalendar(AcademicProgram program, string name, DateOnly start, DateOnly end,
        Action<AcademicCalendar> addEvents, bool publish = true) =>
        Calendar(program, name, start, end, c =>
        {
            c.AddEvent("First Day of School", null, CalendarEventType.TermStart, start, start, true);
            c.AddEvent("Labor Day", null, CalendarEventType.Holiday, new(2026, 9, 7), new(2026, 9, 7), false);
            c.AddEvent("Teacher Professional Development", "No school for students", CalendarEventType.ProfessionalDevelopment, new(2026, 10, 9), new(2026, 10, 9), false);
            c.AddEvent("Thanksgiving Break", null, CalendarEventType.Break, new(2026, 11, 25), new(2026, 11, 27), false);
            c.AddEvent("Winter Break", null, CalendarEventType.Break, new(2026, 12, 21), new(2027, 1, 1), false);
            c.AddEvent("Martin Luther King Jr. Day", null, CalendarEventType.Holiday, new(2027, 1, 18), new(2027, 1, 18), false);
            c.AddEvent("Spring Break", null, CalendarEventType.Break, new(2027, 3, 22), new(2027, 3, 26), false);
            c.AddEvent("Memorial Day", null, CalendarEventType.Holiday, new(2027, 5, 31), new(2027, 5, 31), false);
            c.AddEvent("Last Day of School", null, CalendarEventType.TermEnd, end, end, true);
            addEvents(c);
        }, publish);
}
