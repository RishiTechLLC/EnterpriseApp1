-- Anytime script (re-run on change). Every event with its school, program and calendar, for reports and exports.
CREATE OR ALTER VIEW dbo.vw_CalendarEvents
AS
SELECT
    s.Id                AS SchoolId,
    s.Name              AS SchoolName,
    p.Id                AS ProgramId,
    p.Name              AS ProgramName,
    c.Id                AS CalendarId,
    c.Name              AS CalendarName,
    c.AcademicYear,
    c.Status            AS CalendarStatus,
    e.Id                AS EventId,
    e.Title,
    e.Description,
    e.Type,
    e.StartDate,
    e.EndDate,
    DATEDIFF(DAY, e.StartDate, e.EndDate) + 1 AS DurationDays,
    e.IsInstructionalDay
FROM dbo.CalendarEvents e
JOIN dbo.Calendars c ON c.Id = e.CalendarId
JOIN dbo.Programs p  ON p.Id = c.ProgramId
JOIN dbo.Schools s   ON s.Id = p.SchoolId;
