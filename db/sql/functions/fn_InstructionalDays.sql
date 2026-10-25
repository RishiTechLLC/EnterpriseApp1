-- Anytime script: grate re-runs it whenever this file changes, so keep it idempotent (CREATE OR ALTER).
-- Instructional days of a calendar: weekdays in its range not covered by a non-instructional event.
-- Mirrors AcademicCalendar.CountInstructionalDays() in the domain model.
CREATE OR ALTER FUNCTION dbo.fn_InstructionalDays (@CalendarId UNIQUEIDENTIFIER)
RETURNS TABLE
AS
RETURN
    WITH Digits AS (
        SELECT d FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS v (d)
    ),
    Numbers AS (
        -- 0..999 covers the longest allowed calendar (two years)
        SELECT a.d + b.d * 10 + c.d * 100 AS n
        FROM Digits a CROSS JOIN Digits b CROSS JOIN Digits c
    ),
    Days AS (
        SELECT DATEADD(DAY, Numbers.n, cal.StartDate) AS [Day]
        FROM dbo.Calendars cal
        JOIN Numbers ON Numbers.n <= DATEDIFF(DAY, cal.StartDate, cal.EndDate)
        WHERE cal.Id = @CalendarId
    )
    SELECT COUNT(*) AS InstructionalDays
    FROM Days
    -- 1900-01-01 was a Monday, so 0-4 are Monday-Friday regardless of the server's DATEFIRST setting.
    WHERE DATEDIFF(DAY, '19000101', Days.[Day]) % 7 < 5
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.CalendarEvents e
          WHERE e.CalendarId = @CalendarId
            AND e.IsInstructionalDay = 0
            AND Days.[Day] BETWEEN e.StartDate AND e.EndDate
      );
