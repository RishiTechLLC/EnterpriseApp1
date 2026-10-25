-- Anytime script (re-run on change). One row per calendar with school/program context and totals, for reporting.
CREATE OR ALTER VIEW dbo.vw_CalendarSummary
AS
SELECT
    s.Id            AS SchoolId,
    s.Name          AS SchoolName,
    s.Code          AS SchoolCode,
    p.Id            AS ProgramId,
    p.Name          AS ProgramName,
    p.Code          AS ProgramCode,
    c.Id            AS CalendarId,
    c.Name          AS CalendarName,
    c.AcademicYear,
    c.StartDate,
    c.EndDate,
    c.Status,
    c.PublishedAt,
    ISNULL(ev.EventCount, 0)    AS EventCount,
    ISNULL(ev.HolidayCount, 0)  AS HolidayCount,
    ISNULL(ev.ExamCount, 0)     AS ExamCount,
    days.InstructionalDays
FROM dbo.Calendars c
JOIN dbo.Programs p ON p.Id = c.ProgramId
JOIN dbo.Schools s  ON s.Id = p.SchoolId
OUTER APPLY (
    SELECT
        COUNT(*)                                            AS EventCount,
        SUM(CASE WHEN e.Type = N'Holiday' THEN 1 ELSE 0 END) AS HolidayCount,
        SUM(CASE WHEN e.Type = N'Exam' THEN 1 ELSE 0 END)    AS ExamCount
    FROM dbo.CalendarEvents e
    WHERE e.CalendarId = c.Id
) ev
CROSS APPLY dbo.fn_InstructionalDays(c.Id) days;
